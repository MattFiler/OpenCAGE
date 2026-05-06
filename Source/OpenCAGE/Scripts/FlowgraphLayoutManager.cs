//#define DO_DUMP
//#define DISABLE_FLOWGRAPHS

using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CathodeLib.ObjectExtensions;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Xml.Linq;
using static CATHODE.Scripting.CAGEAnimation;
using static CathodeLib.CompositeFlowgraphTable;

namespace CommandsEditor
{
    //Handles loading vanilla/custom flowgraph layouts, and saving custom layouts
    public class FlowgraphLayoutManager
    {
        private static CompositeFlowgraphTable _preDefinedLayouts = new CompositeFlowgraphTable();
        private static CompositeFlowgraphTable _userDefinedLayouts = new CompositeFlowgraphTable();
        private static CompositeFlowgraphCompatibilityTable _compatibility = new CompositeFlowgraphCompatibilityTable();
        private static CompositePageHistoryTable _history = new CompositePageHistoryTable();

        public static Commands LinkedCommands => _commands;
        private static Commands _commands;
        private static LevelContent _content;

        static FlowgraphLayoutManager()
        {
            byte[] contentCompressed = Properties.Resources.flowgraphs;
            if (File.Exists("data/info.dat"))
                contentCompressed = File.ReadAllBytes("data/info.dat");
            byte[] content = null;

            using (MemoryStream stream = new MemoryStream())
            using (GZipStream compressedStream = new GZipStream(new MemoryStream(contentCompressed), CompressionMode.Decompress))
            {
                compressedStream.CopyTo(stream);
                content = stream.ToArray();
            }
            _preDefinedLayouts = (CompositeFlowgraphTable)CustomTable.ReadTable(content, CustomTableType.COMPOSITE_FLOWGRAPHS);

#if DEBUG && DO_DUMP
            foreach (FlowgraphMeta layout in _preDefinedLayouts.flowgraphs)
            {
                layout.Nodes = layout.Nodes.OrderBy(o => o.EntityGUID).ThenBy(o => o.NodeID).ToList();
                foreach (FlowgraphMeta.NodeMeta node in layout.Nodes)
                {
                    node.ConnectionsOut = node.ConnectionsOut.OrderBy(o => o.ConnectedEntityGUID).ThenBy(o => o.ConnectedNodeID).ThenBy(o => o.ConnectedParameterGUID).ToList();
                    node.UnlinkedPins = node.UnlinkedPins.OrderBy(o => o.ParameterGUID).ToList();
                }

                string outPath = "Layouts/" + layout.CompositeGUID + "/" + layout.Name + ".json";
                Directory.CreateDirectory(outPath.Substring(0, outPath.Length - Path.GetFileName(outPath).Length));
                File.WriteAllText(outPath, JsonConvert.SerializeObject(layout, Newtonsoft.Json.Formatting.Indented, new ShortGuidConverter()));
            }
#endif

            //Always add new composites into the compatibility table
            Singleton.OnCompositeAdded += AddToCompatibilityTable;

            //Make sure to delete any nodes and links to entities the user deletes to avoid causing false incompability flags
            Singleton.OnEntityDeletePending += OnEntityDeletePending;
        }
        private static void AddToCompatibilityTable(Composite composite)
        {
            SaveLayout(null, composite, Path.GetFileName(composite.name)); //Add in a default empty flowgraph
            _compatibility.compatibility_info.Add(new CompositeFlowgraphCompatibilityTable.CompatibilityInfo()
            {
                composite_id = composite.shortGUID,
                flowgraphs_supported = true
            });
        }
        private static void OnEntityDeletePending(Entity entity, Composite composite)
        {
            foreach (FlowgraphMeta flowgraphMeta in _userDefinedLayouts.flowgraphs)
            {
                Composite comp = _commands.GetComposite(flowgraphMeta.CompositeGUID);
                if (comp == null) continue;

                //Remove any nodes that are for the deleted entity, or point to the deleted entity
                List<FlowgraphMeta.NodeMeta> trimmedNodes = new List<FlowgraphMeta.NodeMeta>();
                foreach (FlowgraphMeta.NodeMeta node in flowgraphMeta.Nodes)
                {
                    Entity ent = comp.GetEntityByID(node.EntityGUID);
                    if (ent == null || ent == entity) continue;
                    switch (ent.variant)
                    {
                        case EntityVariant.ALIAS:
                            if (_commands.Utils.GetResolvedTarget(_commands.Utils.ResolveAlias((AliasEntity)ent, comp)).Item2 == entity)
                                continue;
                            break;
                        case EntityVariant.PROXY:
                            if (_commands.Utils.GetResolvedTarget(_commands.Utils.ResolveProxy((ProxyEntity)ent)).Item2 == entity)
                                continue;
                            break;
                    }
                    trimmedNodes.Add(node);
                }
                flowgraphMeta.Nodes = trimmedNodes;

                //Remove any connections that pointed to now removed nodes
                foreach (FlowgraphMeta.NodeMeta node in flowgraphMeta.Nodes)
                {
                    List<FlowgraphMeta.NodeMeta.ConnectionMeta> trimmedConnections = new List<FlowgraphMeta.NodeMeta.ConnectionMeta>();
                    foreach (FlowgraphMeta.NodeMeta.ConnectionMeta connection in node.ConnectionsOut)
                    {
                        if (flowgraphMeta.Nodes.FirstOrDefault(o => o.NodeID == connection.ConnectedNodeID) == null)
                            continue;
                        trimmedConnections.Add(connection);
                    }
                    node.ConnectionsOut = trimmedConnections;
                }
            }
        }

        //Sets if the given composite supports flowgraphs: a composite wouldn't support flowgraphs if it diverges from the saved layout, or has no layout defined
        private static void SetCompatibilityInfo(Composite composite, bool flowgraphs_supported)
        {
            var compatibilityInfo = _compatibility.compatibility_info.FirstOrDefault(o => o.composite_id == composite.shortGUID);
            if (compatibilityInfo == null)
            {
                compatibilityInfo = new CompositeFlowgraphCompatibilityTable.CompatibilityInfo() { composite_id = composite.shortGUID };
                _compatibility.compatibility_info.Add(compatibilityInfo);
            }
            compatibilityInfo.flowgraphs_supported = flowgraphs_supported;
        }

        //Checks to see if a composite has associated flowgraph compatibility info: a composite wouldn't have this if it is being opened for the first time since script editor v3
        public static bool HasCompatibilityInfo(Composite composite)
        {
            return _compatibility.compatibility_info.FirstOrDefault(o => o.composite_id == composite.shortGUID) != null;
        }

        //Checks the given composite against the layout DB to see if the links/entities match
        public static void EvaluateCompatibility(Composite composite)
        {
#if DISABLE_FLOWGRAPHS
            SetCompatibilityInfo(composite, false);
            return;
#endif

            Debug.Log("Flowgraph Manager", "Calculating flowgraph compatibility...");

            //If there are links, make sure they match up with the stored layout (if there is one)
            bool hasLayout = HasLayout(composite);
            if (hasLayout)
            {
                Debug.Log("Flowgraph Manager", "Page(s) exist, checking to see if links match");
                SetCompatibilityInfo(composite, GetLayouts(composite).LinksMatch(composite));
            }
            else
            {
                int links = _commands.Utils.CountLinks(composite);
                if (links == 0)
                {
                    Debug.Log("Flowgraph Manager", "No page exists, but composite has no links, so adding default page - supported!");
                    RemoveAllLayouts(composite);
                    SaveLayout(null, composite, Path.GetFileName(composite.name));
                    SetCompatibilityInfo(composite, true);
                }
                else
                {
                    Debug.Log("Flowgraph Manager", "No page exists, but composite has links defined - unsupported!");
                    SetCompatibilityInfo(composite, false);
                }
            }
        }

        //Checks to see if the given flowgraph is compatible with the Flowgraph system (make sure this has been evaluated first using the method above)
        public static bool IsCompatible(Composite composite)
        {
            if (composite == null)
                return false;

            var info = _compatibility.compatibility_info.FirstOrDefault(o => o.composite_id == composite.shortGUID);
            if (info == null)
                return false;
            return info.flowgraphs_supported;
        }

        //Checks to see if there is at least one finished flowgraph for the given composite
        public static bool HasLayout(Composite composite)
        {
            return _userDefinedLayouts.flowgraphs.FirstOrDefault(o => o.CompositeGUID == composite.shortGUID) != null;
        }

        //Gets all flowgraph layouts for the given composite
        public static List<FlowgraphMeta> GetLayouts(Composite composite)
        {
            return _userDefinedLayouts.flowgraphs.FindAll(o => o.CompositeGUID == composite.shortGUID);
        }

        //Save/add layout to db
        public static FlowgraphMeta SaveLayout(STNodeEditor editor, Composite composite, string name) //NOTE: passing no editor here will produce an empty layout, which could be destructive!
        {
            FlowgraphMeta flowgraphMeta = editor == null ? new FlowgraphMeta() { Name = name, CompositeGUID = composite.shortGUID } : editor.AsFlowgraphMeta(composite, name);
            FlowgraphMeta existingFGM = _userDefinedLayouts.flowgraphs.FirstOrDefault(o => o.Name == flowgraphMeta.Name && o.CompositeGUID == flowgraphMeta.CompositeGUID);
            if (existingFGM != null)
                _userDefinedLayouts.flowgraphs[_userDefinedLayouts.flowgraphs.IndexOf(existingFGM)] = flowgraphMeta;
            else
                _userDefinedLayouts.flowgraphs.Add(flowgraphMeta);
            return flowgraphMeta;
        }

        //Remove a layout from the DB
        public static void RemoveLayout(Composite composite, string name)
        {
            _userDefinedLayouts.flowgraphs.RemoveAll(o => o.CompositeGUID == composite.shortGUID && o.Name == name);
        }
        public static void RemoveAllLayouts(Composite composite)
        {
            _userDefinedLayouts.flowgraphs.RemoveAll(o => o.CompositeGUID == composite.shortGUID);
        }

        //Remember the page that was last selected
        public static void SetSelectedPage(Composite composite, string name)
        {
            if (_history.last_composite_page.ContainsKey(composite.shortGUID))
            {
                _history.last_composite_page[composite.shortGUID] = name;
            }
            else
            {
                _history.last_composite_page.Add(composite.shortGUID, name);
            }
        }
        public static string GetSelectedPage(Composite composite)
        {
            if (_history.last_composite_page.TryGetValue(composite.shortGUID, out string name))
                return name;
            return null;
        }

        public static void LinkCommands(LevelContent content)
        {
            if (_commands != null)
            {
                _commands.OnLoadSuccess -= LoadCustomFlowgraphs;
                _commands.OnSaveSuccess -= SaveCustomFlowgraphs;
            }

            _commands = content?.Level?.Commands;
            _content = content;
            if (_commands == null) return;

            _commands.OnLoadSuccess += LoadCustomFlowgraphs;
            _commands.OnSaveSuccess += SaveCustomFlowgraphs;

            LoadCustomFlowgraphs(_commands.Filepath);
        }

        private static void LoadCustomFlowgraphs(string filepath)
        {
            _userDefinedLayouts = (CompositeFlowgraphTable)CustomTable.ReadTable(filepath, CustomTableType.COMPOSITE_FLOWGRAPHS);
            if (_userDefinedLayouts == null) _userDefinedLayouts = new CompositeFlowgraphTable();
            Debug.Log("Flowgraph Manager", "Loaded " + _userDefinedLayouts.flowgraphs.Count + " custom flowgraph layouts!");
            
            _compatibility = (CompositeFlowgraphCompatibilityTable)CustomTable.ReadTable(filepath, CustomTableType.COMPOSITE_FLOWGRAPH_COMPATIBILITY_INFO);
            if (_compatibility == null) _compatibility = new CompositeFlowgraphCompatibilityTable();
            Debug.Log("Flowgraph Manager", "Loaded " + _compatibility.compatibility_info.Count + " flowgraph compatibility definitions!");

            _history = (CompositePageHistoryTable)CustomTable.ReadTable(filepath, CustomTableType.COMPOSITE_PAGE_HISTORY);
            if (_history == null) _history = new CompositePageHistoryTable();
            Debug.Log("Flowgraph Manager", "Loaded " + _history.last_composite_page.Count + " previously opened pages!");

            _content.IsVanilla = _userDefinedLayouts.flowgraphs.Count + _compatibility.compatibility_info.Count + _history.last_composite_page.Count == 0;

            //Copy the default layouts over for composites in this Commands if they don't already exist
            FlowgraphMeta.SupportedLevel levelID;
            bool hasLevelID = Enum.TryParse(Path.GetFileName(_content.Level.Name).ToUpper(), out levelID);
            List<FlowgraphMeta> newFlowgraphs = new List<FlowgraphMeta>();
#if DEBUG
            HashSet<ShortGuid> mappedComps = new HashSet<ShortGuid>();
#endif
            for (int i = 0; i < _preDefinedLayouts.flowgraphs.Count; i++)
            {
                if (!_preDefinedLayouts.flowgraphs[i].AlwaysUse && (!hasLevelID || !_preDefinedLayouts.flowgraphs[i].SupportedLevels.HasFlag(levelID)))
                    continue;
                if (_commands.Entries.FirstOrDefault(o => o.shortGUID == _preDefinedLayouts.flowgraphs[i].CompositeGUID) == null)
                    continue;
                if (_userDefinedLayouts.flowgraphs.FirstOrDefault(o => o.CompositeGUID == _preDefinedLayouts.flowgraphs[i].CompositeGUID) != null)
                    continue;
                newFlowgraphs.Add(_preDefinedLayouts.flowgraphs[i].Copy());
#if DEBUG
                mappedComps.Add(_preDefinedLayouts.flowgraphs[i].CompositeGUID);
#endif
            }
            _userDefinedLayouts.flowgraphs.AddRange(newFlowgraphs);
#if DEBUG
            Debug.Log("Flowgraph Manager", "Applied " + newFlowgraphs.Count + " suitable new flowgraph layouts, of the " + _preDefinedLayouts.flowgraphs.Count + " available.");
            Debug.Log("Flowgraph Manager", (((float)mappedComps.Count / (float)_commands.Entries.Count) * 100.0f) + "% of the composites in this level have layouts!");
            foreach (Composite comp in _commands.Entries)
            {
                if (mappedComps.Contains(comp.shortGUID))
                    continue;
                
                Debug.Log("Flowgraph Manager", "NO LAYOUT FOR: " + comp.name);
            }
#endif
        }

        private static void SaveCustomFlowgraphs(string filepath)
        {
            CustomTable.WriteTable(filepath, CustomTableType.COMPOSITE_FLOWGRAPHS, _userDefinedLayouts);
            Debug.Log("Flowgraph Manager", "Saved " + _userDefinedLayouts.flowgraphs.Count + " custom flowgraph layouts!");

            CustomTable.WriteTable(filepath, CustomTableType.COMPOSITE_FLOWGRAPH_COMPATIBILITY_INFO, _compatibility);
            Debug.Log("Flowgraph Manager", "Saved " + _compatibility.compatibility_info.Count + " flowgraph compatibility definitions!");

            CustomTable.WriteTable(filepath, CustomTableType.COMPOSITE_PAGE_HISTORY, _history);
            Debug.Log("Flowgraph Manager", "Saved " + _history.last_composite_page.Count + " previously opened pages!");
        }
    }

    public static class FlowgraphManagerUtils
    {
        /* Convert a STNodeEditor graph to a FlowgraphMeta object for saving */
        public static FlowgraphMeta AsFlowgraphMeta(this STNodeEditor editor, Composite composite, string name)
        {
            FlowgraphMeta flowgraphMeta = new FlowgraphMeta();
            flowgraphMeta.CompositeGUID = composite.shortGUID;
            flowgraphMeta.Name = name;

            flowgraphMeta.CanvasPosition = editor.CanvasCenter;
            flowgraphMeta.CanvasScale = editor.CanvasScale;
            flowgraphMeta.Nodes = new List<FlowgraphMeta.NodeMeta>();
            for (int i = 0; i < editor.Nodes.Count; i++)
            {
                STNode node = editor.Nodes[i];
                FlowgraphMeta.NodeMeta nodeMeta = new FlowgraphMeta.NodeMeta();
                nodeMeta.EntityGUID = node.ShortGUID;
                nodeMeta.NodeID = i;

                nodeMeta.Position = node.Location;

                //Check the pins we care about storing links for
                List<STNodeOption> options = node.GetOutputOptions().ToList();
                options.AddRange(node.GetTopOptions());
                for (int y = 0; y < options.Count; y++)
                {
                    List<STNodeOption> connections = options[y].GetConnectedOption();
                    //Store the links (ones that go out)
                    for (int z = 0; z < connections.Count; z++)
                    {
                        STNode connectedNode = connections[z].Owner;
                        nodeMeta.ConnectionsOut.Add(new FlowgraphMeta.NodeMeta.ConnectionMeta()
                        {
                            ParameterGUID = options[y].ShortGUID,
                            ConnectedEntityGUID = connectedNode.ShortGUID,
                            ConnectedNodeID = editor.Nodes.IndexOf(connectedNode),
                            ConnectedParameterGUID = connections[z].ShortGUID,
                        });
                    }
                    //If there were no links, remember the pin anyway
                    if (connections.Count == 0)
                    {
                        nodeMeta.UnlinkedPins.Add(new FlowgraphMeta.NodeMeta.UnlinkedPinMeta()
                        {
                            ParameterGUID = options[y].ShortGUID,
                            PinLocation = (byte)options[y].Location,
                            PinStyle = (byte)options[y].Style,
                        });
                    }
                }

                //Check the pins we don't care about storing links for
                options = node.GetInputOptions().ToList();
                options.AddRange(node.GetBottomOptions());
                for (int y = 0; y < options.Count; y++)
                {
                    List<STNodeOption> connections = options[y].GetConnectedOption();
                    //If there were no links, remember the pin
                    if (connections.Count == 0)
                    {
                        nodeMeta.UnlinkedPins.Add(new FlowgraphMeta.NodeMeta.UnlinkedPinMeta()
                        {
                            ParameterGUID = options[y].ShortGUID,
                            PinLocation = (byte)options[y].Location,
                            PinStyle = (byte)options[y].Style,
                        });
                    }
                }

                flowgraphMeta.Nodes.Add(nodeMeta);
            }

            return flowgraphMeta;
        }

        /* Check a Composite against a set of FlowgraphMetas to see if the links are the same */
        public static bool LinksMatch(this List<FlowgraphMeta> metas, Composite composite)
        {
            List<LinkData> flowgraphLinks = new List<LinkData>();
            for (int i = 0; i < metas.Count; i++)
            {
                for (int x = 0; x < metas[i].Nodes.Count; x++)
                {
                    for (int y = 0; y < metas[i].Nodes[x].ConnectionsOut.Count; y++)
                    {
                        flowgraphLinks.Add(new LinkData(
                            metas[i].Nodes[x].EntityGUID,
                            metas[i].Nodes[x].ConnectionsOut[y].ParameterGUID,
                            metas[i].Nodes[x].ConnectionsOut[y].ConnectedEntityGUID,
                            metas[i].Nodes[x].ConnectionsOut[y].ConnectedParameterGUID)
                        );
                    }
                }
            }

            List<Entity> entities = composite.GetEntities();
            List<LinkData> compositeLinks = new List<LinkData>();
            for (int i = 0; i < entities.Count; i++)
            {
                //Ignore any links that point to missing entities!
                List<EntityConnector> trimmedChildren = new List<EntityConnector>();
                foreach (EntityConnector connector in entities[i].childLinks)
                {
                    if (composite.GetEntityByID(connector.linkedEntityID) == null)
                        continue;
                    trimmedChildren.Add(connector);
                }

                for (int x = 0; x < trimmedChildren.Count; x++)
                {
                    compositeLinks.Add(new LinkData(
                        entities[i].shortGUID,
                        trimmedChildren[x].thisParamID,
                        trimmedChildren[x].linkedEntityID,
                        trimmedChildren[x].linkedParamID)
                    );
                }
            }

            //Do we have the same number of links?
            if (flowgraphLinks.Count != compositeLinks.Count)
            {
                Debug.Log("Flowgraph Manager", "Link count mismatch in page(s) for " + composite.name);
#if DEBUG
                // If in debug mode, output both lists of links so I can easily diff them if needed.
                string dirName = "FGLayoutCheck/" + Path.GetFileName(composite.name.Replace(":", "_"));
                Directory.CreateDirectory(dirName);
                flowgraphLinks = flowgraphLinks.OrderBy(o => o.In.ParameterID.ToString()).ThenBy(o => o.Out.ParameterID.ToString()).ThenBy(o => o.In.EntityID.ToByteString()).ThenBy(o => o.Out.EntityID.ToByteString()).ToList();
                compositeLinks = compositeLinks.OrderBy(o => o.In.ParameterID.ToString()).ThenBy(o => o.Out.ParameterID.ToString()).ThenBy(o => o.In.EntityID.ToByteString()).ThenBy(o => o.Out.EntityID.ToByteString()).ToList();
                File.WriteAllText(dirName + "/FLOWGRAPH LINKS.json", JsonConvert.SerializeObject(flowgraphLinks, Newtonsoft.Json.Formatting.Indented, new ShortGuidConverter()));
                File.WriteAllText(dirName + "/COMPOSITE LINKS.json", JsonConvert.SerializeObject(compositeLinks, Newtonsoft.Json.Formatting.Indented, new ShortGuidConverter()));
#endif
                return false;
            }

            //Now we know we have the same number of links, do the links match? 
            flowgraphLinks = flowgraphLinks.OrderBy(o => o.In.ParameterID.ToString()).ThenBy(o => o.Out.ParameterID.ToString()).ThenBy(o => o.In.EntityID.ToByteString()).ThenBy(o => o.Out.EntityID.ToByteString()).ToList();
            compositeLinks = compositeLinks.OrderBy(o => o.In.ParameterID.ToString()).ThenBy(o => o.Out.ParameterID.ToString()).ThenBy(o => o.In.EntityID.ToByteString()).ThenBy(o => o.Out.EntityID.ToByteString()).ToList();
            for (int i = 0; i < flowgraphLinks.Count; i++)
            {
                if (flowgraphLinks[i] != compositeLinks[i])
                {
                    Debug.Log("Flowgraph Manager", "Link mismatch at index " + i + " in page(s) for " + composite.name);
#if DEBUG
                    // If in debug mode, output both lists of links so I can easily diff them if needed.
                    string dirName = "FGLayoutCheck/" + Path.GetFileName(composite.name.Replace(":", "_"));
                    Directory.CreateDirectory(dirName);
                    File.WriteAllText(dirName + "/FLOWGRAPH LINKS.json", JsonConvert.SerializeObject(flowgraphLinks, Newtonsoft.Json.Formatting.Indented, new ShortGuidConverter()));
                    File.WriteAllText(dirName + "/COMPOSITE LINKS.json", JsonConvert.SerializeObject(compositeLinks, Newtonsoft.Json.Formatting.Indented, new ShortGuidConverter()));
#endif
                    return false;
                }
            }

            //Finally, double check that there aren't any entities missing from the composite.
            for (int i = 0; i < metas.Count; i++)
            {
                for (int x = 0; x < metas[i].Nodes.Count; x++)
                {
                    if (composite.GetEntityByID(metas[i].Nodes[x].EntityGUID) == null)
                    {
                        //If one is missing, check to see if it has any links in/out -> if it doesn't, it's fine, we're not losing anything important.
                        //Just be aware, the Flowgraph UI will need to be able to handle null entities safely.

                        if (metas[i].Nodes[x].ConnectionsOut.Count != 0)
                        {
                            Debug.Log("Flowgraph Manager", "Failed to look up entity " + metas[i].Nodes[x].EntityGUID.ToByteString() + " with connections out for " + composite.name);
                            return false;
                        }

                        //This may seem like a ridiculous level of loops, but really, we should RARELY (or ideally never) get here. 
                        for (int p = 0; p < metas.Count; p++)
                        {
                            for (int c = 0; c < metas[p].Nodes.Count; c++)
                            {
                                for (int y = 0; y < metas[p].Nodes[c].ConnectionsOut.Count; y++)
                                {
                                    if (metas[p].Nodes[c].ConnectionsOut[y].ConnectedEntityGUID == metas[i].Nodes[x].EntityGUID)
                                    {
                                        Debug.Log("Flowgraph Manager", "Failed to look up entity " + metas[i].Nodes[x].EntityGUID.ToByteString() + " with connections in for " + composite.name);
                                        return false;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Debug.Log("Flowgraph Manager", "Links match for " + composite.name);
            return true;
        }

        struct LinkData
        {
            public LinkData(ShortGuid EntityID, ShortGuid ParameterID, ShortGuid LinkedEntityID, ShortGuid LinkedParameterID)
            {
                Out = new Parameter() { EntityID = EntityID, ParameterID = ParameterID };
                In = new Parameter() { EntityID = LinkedEntityID, ParameterID = LinkedParameterID };
            }

            public Parameter Out;
            public Parameter In;

            public struct Parameter
            {
                public ShortGuid EntityID;
                public ShortGuid ParameterID;

                public static bool operator ==(Parameter left, Parameter right)
                {
                    return left.Equals(right);
                }

                public static bool operator !=(Parameter left, Parameter right)
                {
                    return !(left == right);
                }

                public override bool Equals(object obj)
                {
                    if (obj is Parameter other)
                    {
                        return EntityID.Equals(other.EntityID) && ParameterID.Equals(other.ParameterID);
                    }
                    return false;
                }

                public override int GetHashCode()
                {
                    int hashCode = -1506387652;
                    hashCode = hashCode * -1521134295 + EntityID.GetHashCode();
                    hashCode = hashCode * -1521134295 + ParameterID.GetHashCode();
                    return hashCode;
                }
            }

            public static bool operator ==(LinkData left, LinkData right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(LinkData left, LinkData right)
            {
                return !(left == right);
            }

            public override bool Equals(object obj)
            {
                if (obj is LinkData other)
                {
                    return Out == other.Out && In == other.In;
                }
                return false;
            }

            public override int GetHashCode()
            {
                int hashCode = 1047395477;
                hashCode = hashCode * -1521134295 + Out.GetHashCode();
                hashCode = hashCode * -1521134295 + In.GetHashCode();
                return hashCode;
            }
        }
    }
}

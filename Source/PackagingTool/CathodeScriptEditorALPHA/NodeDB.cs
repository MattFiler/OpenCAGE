using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alien_Isolation_Mod_Tools
{
    class NEW_ParamDescriptor
    {
        public byte[] ID;
        public string Description;
    }

    class NodeDB
    {
        static NodeDB()
        {
            cathode_id_map = ReadDB(LocalAsset.GetPathString("CSE_NodeDBs", "cathode_id_map.bin")); //Names for node types, parameters, and enums
            node_friendly_names = ReadDB(LocalAsset.GetPathString("CSE_NodeDBs", "node_friendly_names.bin")); //Names for unique nodes
        }

        //Check the CATHODE data dump for a corresponding name
        public static string GetName(byte[] id)
        {
            if (id == null) return "";
            foreach (NEW_ParamDescriptor db_entry in cathode_id_map) if (db_entry.ID.SequenceEqual(id)) return db_entry.Description;
            return BitConverter.ToString(id);
        }
        public static string GetNodeTypeName(byte[] id, CommandsPAK pak) //This is performed separately to be able to remap nodes that are flowgraphs
        {
            if (id == null) return "";
            foreach (NEW_ParamDescriptor db_entry in cathode_id_map) if (db_entry.ID.SequenceEqual(id)) return db_entry.Description;
            CathodeFlowgraph flow = pak.GetFlowgraph(id); if (flow == null) return BitConverter.ToString(id);
            return flow.name;
        }

        //Check the COMMANDS.BIN dump for node in-editor names
        public static string GetFriendlyName(byte[] id)
        {
            if (id == null) return "";
            foreach (NEW_ParamDescriptor db_entry in node_friendly_names) if (db_entry.ID.SequenceEqual(id)) return db_entry.Description;
            return BitConverter.ToString(id);
        }

        private static List<NEW_ParamDescriptor> ReadDB(string db_path)
        {
            List<NEW_ParamDescriptor> toReturn = new List<NEW_ParamDescriptor>();
            if (!File.Exists(db_path)) return toReturn;

            BinaryReader reader = new BinaryReader(File.OpenRead(db_path));
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                NEW_ParamDescriptor thisDesc = new NEW_ParamDescriptor();
                thisDesc.ID = reader.ReadBytes(4);
                thisDesc.Description = reader.ReadString();
                toReturn.Add(thisDesc);
            }
            reader.Close();

            return toReturn;
        }

        private static List<NEW_ParamDescriptor> cathode_id_map;
        private static List<NEW_ParamDescriptor> node_friendly_names;
    }
}

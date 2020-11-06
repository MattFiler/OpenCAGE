using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alien_Isolation_Mod_Tools
{
    public class ShortGUIDDescriptor
    {
        public byte[] ID;
        public string Description;
    }

    public class NodeDB
    {
        static NodeDB()
        {
#if OPENCAGE_DLL
            //TODO: the unity project needs to be passed the user's A:I folder somehow
            cathode_id_map = ReadDB(@"G:\SteamLibrary\steamapps\common\Alien Isolation\DATA\MODTOOLS\REMOTE_ASSETS\CSE_NodeDBs\cathode_id_map.bin");
            node_friendly_names = ReadDB(@"G:\SteamLibrary\steamapps\common\Alien Isolation\DATA\MODTOOLS\REMOTE_ASSETS\CSE_NodeDBs\node_friendly_names.bin");
#else
            cathode_id_map = ReadDB(LocalAsset.GetPathString("CSE_NodeDBs", "cathode_id_map.bin")); //Names for node types, parameters, and enums
            node_friendly_names = ReadDB(LocalAsset.GetPathString("CSE_NodeDBs", "node_friendly_names.bin")); //Names for unique nodes
#endif
        }

        //Check the CATHODE data dump for a corresponding name
        public static string GetName(byte[] id)
        {
            if (id == null) return "";
            foreach (ShortGUIDDescriptor db_entry in cathode_id_map) if (db_entry.ID.SequenceEqual(id)) return db_entry.Description;
            return BitConverter.ToString(id);
        }
        public static string GetNodeTypeName(byte[] id, CommandsPAK pak) //This is performed separately to be able to remap nodes that are flowgraphs
        {
            if (id == null) return "";
            foreach (ShortGUIDDescriptor db_entry in cathode_id_map) if (db_entry.ID.SequenceEqual(id)) return db_entry.Description;
            CathodeFlowgraph flow = pak.GetFlowgraph(id); if (flow == null) return BitConverter.ToString(id);
            return flow.name;
        }

        //Check the COMMANDS.BIN dump for node in-editor names
        public static string GetFriendlyName(byte[] id)
        {
            if (id == null) return "";
            foreach (ShortGUIDDescriptor db_entry in node_friendly_names) if (db_entry.ID.SequenceEqual(id)) return db_entry.Description;
            return BitConverter.ToString(id);
        }

        private static List<ShortGUIDDescriptor> ReadDB(string db_path)
        {
            List<ShortGUIDDescriptor> toReturn = new List<ShortGUIDDescriptor>();
            if (!File.Exists(db_path)) return toReturn;

            BinaryReader reader = new BinaryReader(File.OpenRead(db_path));
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                ShortGUIDDescriptor thisDesc = new ShortGUIDDescriptor();
                thisDesc.ID = reader.ReadBytes(4);
                thisDesc.Description = reader.ReadString();
                toReturn.Add(thisDesc);
            }
            reader.Close();

            return toReturn;
        }

        private static List<ShortGUIDDescriptor> cathode_id_map;
        private static List<ShortGUIDDescriptor> node_friendly_names;
    }
}

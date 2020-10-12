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
            node_types = ReadDB(LocalAsset.GetPathString("CSE_NodeDBs", "node_desc.bin"));
            node_friendly_names = ReadDB(LocalAsset.GetPathString("CSE_NodeDBs", "node_friendly_names.bin"));
            param_names = ReadDB(LocalAsset.GetPathString("CSE_NodeDBs", "param_desc.bin"));
        }

        public static string GetParameterName(byte[] id)
        {
            if (id == null) return "";
            foreach (NEW_ParamDescriptor db_entry in param_names) if (db_entry.ID.SequenceEqual(id)) return db_entry.Description;
            return BitConverter.ToString(id);
        }

        public static string GetTypeName(byte[] id)
        {
            if (id == null) return "";
            foreach (NEW_ParamDescriptor db_entry in node_types) if (db_entry.ID.SequenceEqual(id)) return db_entry.Description;
            return BitConverter.ToString(id);
        }

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

        private static List<NEW_ParamDescriptor> node_types;
        private static List<NEW_ParamDescriptor> node_friendly_names;
        private static List<NEW_ParamDescriptor> param_names;
    }
}

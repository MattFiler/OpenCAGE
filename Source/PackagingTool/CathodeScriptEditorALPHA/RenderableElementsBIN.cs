using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alien_Isolation_Mod_Tools
{
    public class RenderableElementsBIN
    {
        /* Load the REDS.BIN */
        public RenderableElementsBIN(string pathToBin)
        {
            path_to_bin = pathToBin;

            BinaryReader reader = new BinaryReader(File.OpenRead(path_to_bin));

            renderable_elements.Capacity = reader.ReadInt32();
            ReadEntries(reader);

            reader.Close();
        }

        /* Get RED */
        public RenderableElement GetRenderableElement(int index)
        {
            if (index < 0 || index >= renderable_elements.Count ) return null;
            return renderable_elements[index];
        }

        /* Get REDs */
        public List<RenderableElement> GetRenderableElements()
        {
            return renderable_elements;
        }

        /* Get REDs count */
        public int GetRenderableElementsCount()
        {
            return renderable_elements.Count;
        }

        /* Read all renderable elements entries */
        private void ReadEntries(BinaryReader reader)
        {
            for (int i = 0; i < renderable_elements.Capacity; i++)
            {
                RenderableElement this_entry = new RenderableElement();
                reader.BaseStream.Position += 4;
                this_entry.model_index = reader.ReadInt32();
                reader.BaseStream.Position += 5;
                this_entry.material_index = reader.ReadInt32();
                reader.BaseStream.Position += 1;
                this_entry.LOD_index = reader.ReadInt32();
                this_entry.submesh_count = (int)reader.ReadChar();
                renderable_elements.Add(this_entry);
            }
        }

        private string path_to_bin = "";
        private List<RenderableElement> renderable_elements = new List<RenderableElement>();
    }

    public class RenderableElement
    {
        public int model_index;
        public int material_index;
        public int LOD_index;
        public int submesh_count;
    }
}

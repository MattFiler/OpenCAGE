using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlienPAK
{
    class EntryMaterialMappingsPAK
    {
        public byte[] MapHeader = new byte[4];
        public string MapFilename = "";
        public int MapEntryCoupleCount = 0; //materials will be 2* this number
        public List<string> MapMatEntries = new List<string>();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlienPAK
{
    class EntryCommandsPAK
    {
        public string ScriptName = "";
        public byte[] ScriptID = new byte[4];
        public byte[] ScriptMarker = new byte[4];
        public List<byte> ScriptContent = new List<byte>();
        public int[] ScriptTrailingInts = new int[24]; //These somehow relate to models/entities in the level. Need to look at REDS.BIN!
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlienPAK
{
    class EntryPAK2
    {
        public string Filename = "";
        public int Offset = 0;
        public byte[] Content; //better than a byte list, initialise it with the calculated size
    }
}

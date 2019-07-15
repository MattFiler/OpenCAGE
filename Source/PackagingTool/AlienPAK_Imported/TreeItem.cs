using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlienPAK
{
    public enum TreeItemType {
        EXPORTABLE_FILE, //An exportable file
        LOADED_STRING, //A loaded string (WIP for COMMANDS.PAK)
        PREVIEW_ONLY_FILE, //A read-only file (export not supported yet)
        DIRECTORY //A parent directory listing
    };
    public enum TreeItemIcon
    {
        FOLDER,
        FILE,
        STRING
    };

    public struct TreeItem
    {
        public string String_Value;
        public TreeItemType Item_Type;
    }
}

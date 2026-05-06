using CommandsEditor.Popups.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CommandsEditor.Popups.Configuration_Editors
{
    public partial class VoiceMappingEditor : BaseWindow
    {
        public VoiceMappingEditor() : base()
        {
            InitializeComponent();
            // CUSTOMCHARACTERVOICETYPEMAPPINGS.BIN in CHR_INFO -> it's an XML file

            // Note that there are also 2 other BIN files here that CathodeLib supports that aren't editable yet: CUSTOMCHARACTERASSETDATA is already used by EditCharacterAssets.

            // todo - when i implement this i need to add it to the backup tool
        }
    }
}

using AlienPAK;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools
{
    public partial class ContentTools_Loadscreen : Form
    {
        private AlienPAK_Imported AlienPAK_Reference;
        private SoundTool SoundTool_Reference;
        private CSE_Alpha CathodeEditorWinform_Reference;
        public ContentTools_Loadscreen(AlienPAK_Imported alienpak_ref = null, SoundTool soundtool_ref = null, CSE_Alpha ce_ref = null)
        {
            AlienPAK_Reference = alienpak_ref;
            SoundTool_Reference = soundtool_ref;
            CathodeEditorWinform_Reference = ce_ref;
            InitializeComponent();
        }
        private void form1_Shown(object sender, EventArgs e)
        {
            this.Refresh();
            if (AlienPAK_Reference != null) AlienPAK_Reference.StartLoadingContent();
            if (SoundTool_Reference != null) SoundTool_Reference.StartLoadingContent();
            if (CathodeEditorWinform_Reference != null) CathodeEditorWinform_Reference.StartLoadingContent();
        }
    }
}

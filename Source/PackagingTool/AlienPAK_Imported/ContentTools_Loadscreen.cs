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
        private AlienPAK_Imported toolRef;
        public ContentTools_Loadscreen(AlienPAK_Imported loader)
        {
            toolRef = loader;
            InitializeComponent();
        }
        private void form1_Shown(object sender, EventArgs e)
        {
            this.Refresh();
            toolRef.StartLoadingContent();
        }
    }
}

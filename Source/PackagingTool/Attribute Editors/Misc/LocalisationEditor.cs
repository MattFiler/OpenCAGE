using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools.Attribute_Editors.Misc
{
    public partial class LocalisationEditor : Form
    {
        LocalisationHandler localisationUtility = new LocalisationHandler();

        public LocalisationEditor()
        {
            InitializeComponent();
        }

        /* TEST */
        private void loadDemo_Click(object sender, EventArgs e)
        {
            stringOut.Text = localisationUtility.GetLocalisedString(stringID.Text, LocalisationHandler.AYZ_Lang.ENGLISH).TextValue;
        }
    }
}

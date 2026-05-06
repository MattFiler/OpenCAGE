using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CATHODE.Scripting;
using CathodeLib;
using CATHODE;
using System.Numerics;

namespace OpenCAGE.Popups.UserControls
{
    public partial class GUI_Resource_Default : ResourceUserControl
    {
        public GUI_Resource_Default() : base()
        {
            InitializeComponent();
        }

        public override void PopulateUI(ResourceReference resource)
        {
            groupBox1.Text = resource.resource_type.ToString();
        }
    }
}

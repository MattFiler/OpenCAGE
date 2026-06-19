using CATHODE;
using CATHODE.Scripting;
using OpenCAGE.UserControls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Popups.UserControls
{
    public partial class ResourceUserControl : UserControl
    {
        protected LevelContent Content => Singleton.Editor?.CompositeBrowser?.Content;

        public ResourceUserControl()
        {
            InitializeComponent();
        }

        public virtual void PopulateUI(ResourceReference resource)
        {

        }
    }
}

using CATHODE;
using CATHODE.Scripting;
using CommandsEditor.UserControls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CommandsEditor.Popups.UserControls
{
    public partial class ResourceUserControl : UserControl
    {
        protected LevelContent Content => Singleton.Editor?.CommandsDisplay?.Content;

        public ResourceUserControl()
        {
            InitializeComponent();
        }

        public virtual void PopulateUI(ResourceReference resource)
        {

        }
    }
}

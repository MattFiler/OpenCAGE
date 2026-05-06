using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    public partial class About : BaseWindow
    {
        public About() : base()
        {
            InitializeComponent();

            aboutHost.Child = new AboutWPF();
        }
    }
}

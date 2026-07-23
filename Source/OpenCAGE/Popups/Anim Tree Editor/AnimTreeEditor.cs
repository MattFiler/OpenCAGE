using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.AnimTrees
{
    public partial class AnimTreeEditor : Form
    {
        public static DockPanel DockPanel;

        public AnimTreeEditor()
        {
            InitializeComponent();

            DockPanel = this.dockPanel;

            AnimationSets sets = new AnimationSets();
            sets.Show(dockPanel, DockState.DockLeft); //todo: non-closable
        }
    }
}

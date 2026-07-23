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

        private AnimationSets _animationSets;

        public AnimTreeEditor()
        {
            InitializeComponent();

            DockPanel = this.dockPanel;

            _animationSets = new AnimationSets();
            _animationSets.Show(dockPanel, DockState.DockLeft); //todo: non-closable
        }

        private void saveBtn_Click(object sender, EventArgs e)
        {
            if (_animationSets == null)
                return;

            saveBtn.Enabled = false;
            try
            {
                if (_animationSets.SaveAll())
                    MessageBox.Show("Animation trees saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                saveBtn.Enabled = true;
            }
        }
    }
}

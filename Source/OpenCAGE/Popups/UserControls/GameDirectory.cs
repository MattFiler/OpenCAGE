using CathodeLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Popups.UserControls
{
    public partial class GameDirectory : UserControl
    {
        public Action<string> OnSetDefault;
        public Action<string> OnRemoved;

        public bool IsDefault { get { return _isDefault; } }
        private bool _isDefault = false;

        public GameDirectory()
        {
            InitializeComponent();
        }

        public void Populate(string path)
        {
            gameInstallDir.Text = path;
            gameVersion.Text = PatchManager.GetPlatform(path).ToString();
        }

        public void MarkAsDefault(bool isDefault = true)
        {
            setAsDefault.Enabled = !isDefault;
            _isDefault = isDefault;
        }

        private void setAsDefault_Click(object sender, EventArgs e)
        {
            OnSetDefault?.Invoke(gameInstallDir.Text);
        }

        private void openInEditor_Click(object sender, EventArgs e)
        {
            Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location, "-pathToAI=\"" + gameInstallDir.Text + "\"");
        }

        private void removeFromList_Click(object sender, EventArgs e)
        {
            OnRemoved?.Invoke(gameInstallDir.Text);
        }
    }
}

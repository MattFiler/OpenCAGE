using CommandsEditor.Popups.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CommandsEditor.ConfigEditors
{
    public partial class PermaSoundbankPopup : BaseWindow
    {
        public Action<string> OnSoundbankAdded;

        public PermaSoundbankPopup() : base()
        {
            InitializeComponent();

            comboBox1.BeginUpdate();
            string[] files = Directory.GetFiles(Singleton.PathToAI + @"\DATA\SOUND\", "*.BNK");
            foreach (string file in files)
            {
                comboBox1.Items.Add(Path.GetFileNameWithoutExtension(file));
            }
            comboBox1.EndUpdate();

            comboBox1.SelectedIndex = 0;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OnSoundbankAdded?.Invoke(comboBox1.Text);
            this.Close();
        }
    }
}

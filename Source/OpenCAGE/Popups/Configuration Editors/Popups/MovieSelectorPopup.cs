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
    public partial class MovieSelectorPopup : BaseWindow
    {
        public Action<string> OnMovieSelected;

        public MovieSelectorPopup() : base()
        {
            InitializeComponent();

            comboBox1.BeginUpdate();
            string[] files = Directory.GetFiles(Singleton.PathToAI + @"\DATA\UI\MOVIES\", "*.*");
            foreach (string file in files)
            {
                comboBox1.Items.Add(Path.GetFileName(file));
            }
            comboBox1.EndUpdate();

            comboBox1.SelectedIndex = 0;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OnMovieSelected?.Invoke(comboBox1.Text);
            this.Close();
        }
    }
}

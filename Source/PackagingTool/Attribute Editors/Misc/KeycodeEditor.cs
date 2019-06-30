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

namespace Alien_Isolation_Mod_Tools.Attribute_Editors.Misc
{
    public partial class KeycodeEditor : Form
    {
        Directories AlienDirectories = new Directories();

        public KeycodeEditor()
        {
            InitializeComponent();
        }

        /* Load keycode value after selection from list */
        private void keycodeTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (keycodeTree.SelectedNode == null || keycodeTree.SelectedNode.Tag == null)
            {
                keycodeValue.Text = "0000";
                return;
            }

            keycodeValue.Text = GetSelectedKeycode();
        }

        /* Save keybind to selection */
        private void saveKeycode_Click(object sender, EventArgs e)
        {
            if (keycodeTree.SelectedNode == null || keycodeTree.SelectedNode.Tag == null)
            {
                MessageBox.Show("Cannot save keycode without selecting a useage from the list first!", "Cannot save unselected keybind", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (SetSelectedKeycode(keycodeValue.Text))
            {
                MessageBox.Show("Keycode saved!", "Saved new keycode", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("An error occurred while saving the new keycode!", "An error occurred", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /* Get PAK path / offset */
        private string GetCommandsPakPath()
        {
            string[] info = keycodeTree.SelectedNode.Tag.ToString().Split(' ');
            return AlienDirectories.GameDirectoryRoot() + @"\DATA\ENV\PRODUCTION\" + info[0] + @"\WORLD\COMMANDS.PAK";
        }
        private int GetCommandsPakOffset()
        {
            string[] info = keycodeTree.SelectedNode.Tag.ToString().Split(' ');
            return Convert.ToInt32(info[1]);
        }

        /* Get keycode from selected tree node */
        private string GetSelectedKeycode()
        {
            string keycode = "";

            BinaryReader reader = new BinaryReader(File.OpenRead(GetCommandsPakPath()));
            reader.BaseStream.Position = GetCommandsPakOffset();
            for (int i = 0; i < 4; i++)
            {
                keycode += reader.ReadChar();
            }
            reader.Close();

            return keycode;
        }

        /* Open Keycode Writer */
        private bool SetSelectedKeycode(string keycode)
        {
            try
            {
                byte[] commandsPAK = File.ReadAllBytes(GetCommandsPakPath());
                for (int i = 0; i < 4; i++)
                {
                    commandsPAK[GetCommandsPakOffset() + i] = BitConverter.GetBytes(keycode[i])[0];
                }
                File.WriteAllBytes(GetCommandsPakPath(), commandsPAK);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

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
        LocalisationHandler localisationUtility = new LocalisationHandler();

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
                MessageBox.Show("Cannot save keycode without selecting a useage from the list first!", "No keycode selected", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (keycodeValue.Text.Length != 4)
            {
                MessageBox.Show("Keycode must be four numbers!", "Keycode too short", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            return AlienDirectories.GameDirectoryRoot() + @"\DATA\ENV\PRODUCTION\" + KeycodeInfoArray()[0] + @"\WORLD\COMMANDS.PAK";
        }
        private int GetCommandsPakOffset()
        {
            return Convert.ToInt32(KeycodeInfoArray()[1]);
        }

        /* Get selected keycode info */
        private string[] KeycodeInfoArray()
        {
            return keycodeTree.SelectedNode.Tag.ToString().Split(' ');
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
                //Rewrite code for UI (TEXT)
                for (int i = 0; i < Convert.ToInt32(KeycodeInfoArray()[2]); i++)
                {
                    for (int x = 0; x < localisationUtility.languageFolders.Length; x++)
                    {
                        LocalisedText text = localisationUtility.GetLocalisedString(KeycodeInfoArray()[3 + i], (LocalisationHandler.AYZ_Lang)x);
                        text.TextValue = text.TextValue.Replace(GetSelectedKeycode(), keycode);
                        localisationUtility.UpdateLocalisedString(text);
                    }
                }

                //Rewrite code in script (COMMANDS.PAK)
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

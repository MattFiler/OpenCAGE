using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools.Attribute_Editors.Misc
{
    public partial class LocalisationEditor : Form
    {
        LocalisationHandler localisationUtility = new LocalisationHandler();

        public LocalisationEditor()
        {
            InitializeComponent();
        }

        /* Populate possible languages on load */
        private void LocalisationEditor_Load(object sender, EventArgs e)
        {
            foreach (string language in localisationUtility.languageFolders)
            {
                selectedLanguage.Items.Add(language);
            }
            selectedLanguage.SelectedIndex = 1;
        }

        /* Repopulate form for selected language (this might need to be optimised) */
        private void selectedLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            List<LocalisedText> allTextIDs = localisationUtility.GetAllIDs((LocalisationHandler.AYZ_Lang)selectedLanguage.SelectedIndex);

            //Add the "mission IDs" (filenames) as root nodes
            localisationTree.Nodes.Clear();
            foreach (LocalisedText thisTextID in allTextIDs)
            {
                bool shouldAdd = true;
                foreach (TreeNode existingMissionNode in localisationTree.Nodes)
                {
                    if (existingMissionNode.Text == thisTextID.MissionID)
                    {
                        shouldAdd = false;
                        break;
                    }
                }
                if (shouldAdd)
                {
                    TreeNode missionNode = new TreeNode(thisTextID.TextID);
                    missionNode.Tag = "MISSION";
                    localisationTree.Nodes.Add(missionNode);
                }
            }

            //Add each text ID to its respective "mission id"
            foreach (LocalisedText thisTextID in allTextIDs)
            {
                foreach (TreeNode existingMissionNode in localisationTree.Nodes)
                {
                    if (existingMissionNode.Text == thisTextID.MissionID)
                    {
                        TreeNode stringNode = new TreeNode(thisTextID.TextID);
                        stringNode.Tag = "STRING";
                        existingMissionNode.Nodes.Add(stringNode);
                        break;
                    }
                }
            }
        }

        /* Update string preview when an ID is selected */
        private void localisationTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (localisationTree.SelectedNode == null || localisationTree.SelectedNode.Tag.ToString() != "STRING")
            {
                stringOut.Text = "";
                return;
            }

            stringOut.Text = localisationUtility.GetLocalisedString(localisationTree.SelectedNode.Text, LocalisationHandler.AYZ_Lang.ENGLISH).TextValue;
        }
    }
}

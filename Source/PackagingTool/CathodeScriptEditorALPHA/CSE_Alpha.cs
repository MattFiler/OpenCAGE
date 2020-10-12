using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools
{
    public partial class CSE_Alpha : Form
    {
        private CommandsPAK commandsPAK;
        private TreeUtility treeHelper;
        private ToolPaths Folders = new ToolPaths();
        private ContentTools_Loadscreen loadscreen;

        public CSE_Alpha()
        {
            InitializeComponent();
            treeHelper = new TreeUtility(FileTree);

            //Populate available maps
            List<string> all_map_dirs = MapDirectories.GetAvailable();
            env_list.Items.Clear();
            foreach (string map in all_map_dirs) env_list.Items.Add(map);
            env_list.SelectedIndex = 0;
        }

        /* Load a COMMANDS.PAK into the editor */
        private void load_commands_pak_Click(object sender, EventArgs e)
        {
            //Reset all UI here
            FileTree.Nodes.Clear();

            //Call loadscreen, which then calls StartLoadingContent below when shown
            loadscreen = new ContentTools_Loadscreen(null, null, this);
            loadscreen.Show();
        }
        public void StartLoadingContent()
        {
            //Load COMMANDS.PAK and populate file tree
            commandsPAK = new CommandsPAK(Folders.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/ENV/PRODUCTION/" + env_list.SelectedItem + "/WORLD/COMMANDS.PAK");
            treeHelper.UpdateFileTree(commandsPAK.GetFlowgraphs());

            loadscreen.Close();
        }

        /* Save the current edits */
        private void save_commands_pak_Click(object sender, EventArgs e)
        {

        }
    }
}

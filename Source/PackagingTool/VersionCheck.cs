using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools
{
    public partial class VersionCheck : Form
    {
        public VersionCheck()
        {
            InitializeComponent();
        }

        private void VersionCheck_Load(object sender, EventArgs e)
        {
            try
            {
                //Get current Github version
                WebClient webClient = new WebClient();
                Stream webStream = webClient.OpenRead("https://raw.githubusercontent.com/MattFiler/LegendPlugin/master/Source/version.txt");
                StreamReader webRead = new StreamReader(webStream);
                string LatestVersion = webRead.ReadToEnd();

                //Check against current version
                if (Application.ProductVersion != LatestVersion)
                {
                    MessageBox.Show("A new version of the Alien: Isolation Mod Tools is available for download." + Environment.NewLine + "Please update to recieve the latest features!", "Update required.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

                    //Close app
                    Application.Exit();
                    Environment.Exit(0);
                }
            }
            catch (Exception response)
            {
                //No internet?
                //MessageBox.Show(response.ToString());
            }

            //Close version checker
            this.Close();
        }
    }
}

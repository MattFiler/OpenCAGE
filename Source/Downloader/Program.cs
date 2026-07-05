using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace Downloader
{
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Process.Start("https://store.steampowered.com/app/3367530/OpenCAGE/");
            Process.Start("steam://launch/3367530");
            if (MessageBox.Show("" +
                "Thanks for downloading OpenCAGE!\n\n" +
                "" +
                "As of July 2026, ModDB/Nexus/GitHub installs of OpenCAGE have been deprecated to allow support for a range of new functionality.\n\n" +
                "" +
                "To continue using OpenCAGE you must install the tools via Steam. No data will be lost, you can continue as normal once installed!\n\n" +
                "" +
                "", "OpenCAGE is now available via Steam!", MessageBoxButtons.OK, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                return;
            }
        }
    }
}

using CathodeLib;
using System.IO;
using System.Windows.Forms;

namespace OpenCAGE
{
    public enum GameDirectorySelectResult
    {
        Success,
        Cancelled,
        Invalid
    }

    public static class GameDirectorySelector
    {
        /* Prompt the user to pick a game install, then verify with Utilities.IsGameDirectoryValid */
        public static GameDirectorySelectResult TryPromptForGameDirectory(out string path)
        {
            path = null;

            if (Singleton.DontRequireAIexe)
            {
                MessageBox.Show("Please locate your Alien: Isolation install folder.", "OpenCAGE Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select your Alien: Isolation install folder";
                    dialog.ShowNewFolderButton = false;
                    if (dialog.ShowDialog() != DialogResult.OK)
                        return GameDirectorySelectResult.Cancelled;

                    if (Utilities.IsGameDirectoryValid(dialog.SelectedPath))
                    {
                        path = Path.GetFullPath(dialog.SelectedPath);
                        return GameDirectorySelectResult.Success;
                    }
                }
            }
            else
            {
                MessageBox.Show("Please locate your Alien: Isolation executable (AI.exe).", "OpenCAGE Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Filter = "Applications (*.exe)|AI.exe";
                    if (dialog.ShowDialog() != DialogResult.OK)
                        return GameDirectorySelectResult.Cancelled;

                    if (Utilities.IsGameDirectoryValid(Path.GetDirectoryName(dialog.FileName)))
                    {
                        path = Path.GetFullPath(Path.GetDirectoryName(dialog.FileName));
                        return GameDirectorySelectResult.Success;
                    }
                }
            }

            return GameDirectorySelectResult.Invalid;
        }
    }
}

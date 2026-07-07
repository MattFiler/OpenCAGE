using CathodeLib;
using OpenCAGE.Popups.UserControls;
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

namespace OpenCAGE.Popups
{
    public partial class GameDirectoryManager : Form
    {
        Dictionary<string, GameDirectory> _directoryUIs = new Dictionary<string, GameDirectory>();

        public GameDirectoryManager()
        {
            InitializeComponent();

            List<string> directories = new List<string>();
            foreach (string directory in SettingsManager.GetStringArray(Settings.GameDirectories))
            {
                string path = Path.GetFullPath(directory);

                if (!Utilities.IsGameDirectoryValid(path) || directories.Contains(path))
                    continue;
                directories.Add(path);
            }
            if (!directories.Contains(Path.GetFullPath(Singleton.PathToAI)))
                directories.Add(Path.GetFullPath(Singleton.PathToAI));

            for (int i = 0; i < directories.Count; i++) 
            {
                GameDirectory ui = AddInstallUI(directories[i]);
                if (i == 0)
                    _directoryUIs[directories[i]].MarkAsDefault(true);
            }

            launchWithoutViewport.Checked = SettingsManager.GetBool(Settings.LaunchChildrenWithoutViewport) || !Singleton.ViewportEnabled;

            UpdateSavedDirectories();
            this.FormClosing += GameDirectoryManager_FormClosing;
        }

        private GameDirectory AddInstallUI(string directory)
        {
            GameDirectory directoryUI = new GameDirectory();
            directoryUI.Populate(directory);
            directoryUI.OnSetDefault += OnSetDefault;

            _directoryUIs.Add(directory, directoryUI);
            flowLayoutPanel1.Controls.Add(directoryUI);

            return directoryUI;
        }

        private void GameDirectoryManager_FormClosing(object sender, FormClosingEventArgs e)
        {
            foreach (KeyValuePair<string, GameDirectory> ui in _directoryUIs)
            {
                ui.Value.OnSetDefault -= OnSetDefault;
            }
        }

        private void OnSetDefault(string path)
        {
            foreach (KeyValuePair<string, GameDirectory> ui in _directoryUIs)
            {
                ui.Value.MarkAsDefault(ui.Key == path);
            }
            UpdateSavedDirectories();
        }

        private void registerNew_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Please locate your Alien: Isolation executable (AI.exe).", "OpenCAGE Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Applications (*.exe)|AI.exe";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (Utilities.IsGameDirectoryValid(Path.GetDirectoryName(dialog.FileName)))
                    {
                        string newDirectory = Path.GetFullPath(Path.GetDirectoryName(dialog.FileName));

                        if (_directoryUIs.ContainsKey(newDirectory))
                        {
                            MessageBox.Show("Cannot add the same game install twice!", "Failed to add.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        AddInstallUI(newDirectory);
                        if (_directoryUIs.Count == 1)
                            _directoryUIs[newDirectory].MarkAsDefault(true);
                        UpdateSavedDirectories();
                    }
                    else
                    {
                        MessageBox.Show("Failed to add the selected path, could not detect a valid Alien: Isolation install!", "Failed to add.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void UpdateSavedDirectories()
        {
            List<string> directories = new List<string>();
            foreach (KeyValuePair<string, GameDirectory> ui in _directoryUIs)
            {
                if (ui.Value.IsDefault)
                    directories.Insert(0, ui.Key);
                else
                    directories.Add(ui.Key);
            }
            SettingsManager.SetStringArray(Settings.GameDirectories, directories.ToArray());
        }

        private void launchWithoutViewport_CheckedChanged(object sender, EventArgs e)
        {
            SettingsManager.SetBool(Settings.LaunchChildrenWithoutViewport, launchWithoutViewport.Checked);
        }
    }
}

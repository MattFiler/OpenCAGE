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

            string[] directories = SettingsManager.GetStringArray(Settings.GameDirectories);
            for (int i = 0; i < directories.Length; i++) 
            {
                AddInstallUI(directories[i]);
                if (i == 0)
                    _directoryUIs[directories[i]].MarkAsDefault(true);
            }

            this.FormClosing += GameDirectoryManager_FormClosing;
        }

        private void AddInstallUI(string directory)
        {
            GameDirectory directoryUI = new GameDirectory();
            directoryUI.Populate(directory);
            directoryUI.OnSetDefault += OnSetDefault;
            directoryUI.OnRemoved += OnRemoved;

            _directoryUIs.Add(directory, directoryUI);
            flowLayoutPanel1.Controls.Add(directoryUI);
        }

        private void GameDirectoryManager_FormClosing(object sender, FormClosingEventArgs e)
        {
            foreach (KeyValuePair<string, GameDirectory> ui in _directoryUIs)
            {
                ui.Value.OnSetDefault -= OnSetDefault;
                ui.Value.OnRemoved -= OnRemoved;
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

        private void OnRemoved(string path)
        {
            _directoryUIs[path].OnSetDefault -= OnSetDefault;
            _directoryUIs[path].OnRemoved -= OnRemoved;

            flowLayoutPanel1.Controls.Remove(_directoryUIs[path]);
            _directoryUIs.Remove(path);

            if (_directoryUIs.Count > 0)
            {
                bool hasDefault = false;
                foreach (KeyValuePair<string, GameDirectory> ui in _directoryUIs)
                {
                    if (ui.Value.IsDefault)
                    {
                        hasDefault = true;
                        break;
                    }
                }
                if (!hasDefault)
                {
                    foreach (KeyValuePair<string, GameDirectory> ui in _directoryUIs)
                    {
                        ui.Value.MarkAsDefault(true);
                        break;
                    }
                }
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
                        string newDirectory = Path.GetDirectoryName(dialog.FileName);
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
    }
}

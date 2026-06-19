using AlienPAK;
using CathodeLib;
using OpenCAGE.Backups;
using OpenCAGE.Popups;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace OpenCAGE
{
    // imported from backup tool

    public partial class LevelBackupManager : Form
    {
        AlienLevel level = null;

        public LevelBackupManager()
        {
            InitializeComponent();

            if (!Directory.Exists(Singleton.PathToAI + "/DATA/MODTOOLS/BACKUPS"))
            {
                MessageBox.Show("Welcome to the OpenCAGE Level Backup Manager! It is recommended to create a backup of all levels when they are in an unmodified state, to be able to revert back to later.", "Welcome!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            EditorUtils.PopulateLevelDropdown(levelList);

            RefreshList();
        }

        /* Populate the UI for all backups in the selected level */
        private void RefreshList()
        {
            backupList.Items.Clear();
            for (int i = 0; i < level.Backups.Count; i++)
            {
                int changeCount = i == 0 ? level.Backups[i].GUIDs.Count : level.CalculateDiff(level.Backups[i - 1], level.Backups[i]);
                backupList.Items.Add(new ListViewItem(new string[] { level.Backups[i].Name, level.Backups[i].Date, changeCount + " Files Modified" }));
            }
            backupLabel.Text = "Create Backup (" + level.CalculateDiff(level.Backups.Count == 0 ? null : level.Backups[level.Backups.Count - 1]) + " Changes)";
        }

        /* Select a new level */
        private void levelList_SelectedIndexChanged(object sender, EventArgs e)
        {
            level = new AlienLevel(levelList.SelectedItem.ToString());
            RefreshList();
        }

        /* Create a backup of the currently selected level */
        private void saveBackup_Click(object sender, EventArgs e)
        {
            if (backupName.Text == "")
            {
                MessageBox.Show("Please enter a backup name!");
                return;
            }

            if (IsLevelActivelyBeingEdited(level.Name))
            {
                if (MessageBox.Show("This level is currently open in the script editor, would you like to save it before backing up?", "Save level?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    Singleton.Editor.SaveLevel(false);
            }

            this.Cursor = Cursors.WaitCursor;
            level.CreateBackup(backupName.Text);
            RefreshList();
            this.Cursor = Cursors.Default;

            Steam.UnlockAchievement(Steam.Achievements.BACKUP_CREATED);
            MessageBox.Show("Backup successfully created!", "Backup created", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /* Restore the selected backup for the selected level */
        private void restoreSelectedBackup(object sender, EventArgs e)
        {
            if (backupList.SelectedItems.Count != 1)
            {
                MessageBox.Show("Please select one backup from the list to restore.", "None or multiple selected!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            EditorUtils.CloseAI();

            this.Cursor = Cursors.WaitCursor;
            if (level.RestoreBackup(level.Backups[backupList.SelectedItems[0].Index].ID))
            {
                RefreshList();
                MessageBox.Show("Backup successfully restored!", "Restored backup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (IsLevelActivelyBeingEdited(level.Name))
                {
                    if (MessageBox.Show("Would you like to reload the script editor?", "Reload level?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        Singleton.Editor.LoadLevel(level.Name);
                }
            }
            else
            {
                MessageBox.Show("Failed to restore backup!\nPlease close anything that may be using the files within the level, and try again.", "Failed!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            this.Cursor = Cursors.Default;
        }

        /* Delete the selected backups for the selected level */
        private void deleteSelectedBackups_Click(object sender, EventArgs e)
        {
            if (backupList.CheckedItems.Count == 0)
            {
                MessageBox.Show("Please check at least one backup from the list to delete.", "None checked!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("You are about to delete " + backupList.CheckedItems.Count + " backups. Are you sure?", "About to delete...", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            this.Cursor = Cursors.WaitCursor;

            List<AlienLevel.AlienBackup> toDelete = new List<AlienLevel.AlienBackup>();
            for (int i = 0; i < backupList.CheckedItems.Count; i++)
                toDelete.Add(level.Backups[backupList.CheckedItems[i].Index]);
            for (int i = 0; i < toDelete.Count; i++)
                level.DeleteBackup(toDelete[i].ID);
            RefreshList();

            this.Cursor = Cursors.Default;
        }

        /* Backup every level as they stand right now! */
        private void backupAllNow_Click(object sender, EventArgs e)
        {
            if (IsLevelActivelyBeingEdited())
            {
                if (MessageBox.Show("A level is currently open in the script editor, would you like to save it before backing up?", "Save level?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    Singleton.Editor.SaveLevel(false);
            }

            MessageBox.Show("Complete backup starting - this will take some time!\nPlease do not close the tool.", "Backup starting!", MessageBoxButtons.OK, MessageBoxIcon.Information);

            this.Cursor = Cursors.WaitCursor;

            List<string> levels = Level.GetLevels(Singleton.PathToAI);
            //Parallel.ForEach(levels, (levelName) =>
            //{
            //    AlienLevel lvl = new AlienLevel(levelName);
            //    lvl.CreateBackup(lvl.Backups.Count == 0 ? "First backup" : "Automated backup across all levels");
            //});
            //Doing this in parallel requires so much RAM I don't think it's really feasible for most modders that use these tools.
            foreach (string levelName in levels)
            {
                AlienLevel lvl = new AlienLevel(levelName);
                lvl.CreateBackup(lvl.Backups.Count == 0 ? "First backup" : "Automated backup across all levels");
            }

            level = new AlienLevel(levelList.SelectedItem.ToString());
            RefreshList();
            this.Cursor = Cursors.Default;

            Steam.UnlockAchievement(Steam.Achievements.BACKUP_CREATED);
        }

        private bool IsLevelActivelyBeingEdited(string levelName = "")
        {
            if (Singleton.Editor.CompositeBrowser?.Content == null)
                return false;
            if (levelName == "")
                return true;
            return Singleton.Editor.CompositeBrowser.Content.Level.Name.ToUpper().Replace("\\", "/") == levelName.ToUpper().Replace("\\", "/");
        }

        ResetConfigs _configReset = null;
        private void revertConfigs_Click(object sender, EventArgs e)
        {
            if (_configReset != null)
            {
                _configReset.FormClosed -= _configReset_FormClosed;
                _configReset.Close();
            }

            _configReset = new ResetConfigs();
            _configReset.Show();
            _configReset.FormClosed += _configReset_FormClosed;
        }
        private void _configReset_FormClosed(object sender, FormClosedEventArgs e)
        {
            _configReset = null;
        }
    }
}

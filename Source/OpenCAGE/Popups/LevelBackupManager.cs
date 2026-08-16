using AlienPAK;
using CathodeLib;
using OpenCAGE.Backups;
using OpenCAGE.Popups;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    // imported from backup tool

    public partial class LevelBackupManager : Form
    {
        AlienLevel level = null;

        const int BackupCooldownMs = 1000;
        DateTime _lastBackupUtc = DateTime.MinValue;
        Timer _backupCooldownTimer;
        bool _isBusy;

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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isBusy)
            {
                e.Cancel = true;
                MessageBox.Show("Please wait for the backup to finish.", "Backup in progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_backupCooldownTimer != null)
            {
                _backupCooldownTimer.Stop();
                _backupCooldownTimer.Dispose();
                _backupCooldownTimer = null;
            }
            base.OnFormClosed(e);
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

        bool TryBeginBackupAction()
        {
            if (_isBusy || !saveBackup.Enabled || !backupAllNow.Enabled)
                return false;

            if ((DateTime.UtcNow - _lastBackupUtc).TotalMilliseconds < BackupCooldownMs)
                return false;

            saveBackup.Enabled = false;
            backupAllNow.Enabled = false;
            return true;
        }

        void EndBackupAction()
        {
            _lastBackupUtc = DateTime.UtcNow;

            if (_backupCooldownTimer != null)
            {
                _backupCooldownTimer.Stop();
                _backupCooldownTimer.Dispose();
            }

            _backupCooldownTimer = new Timer { Interval = BackupCooldownMs };
            _backupCooldownTimer.Tick += (s, e) =>
            {
                _backupCooldownTimer.Stop();
                _backupCooldownTimer.Dispose();
                _backupCooldownTimer = null;

                if (IsDisposed || _isBusy)
                    return;

                saveBackup.Enabled = true;
                backupAllNow.Enabled = true;
            };
            _backupCooldownTimer.Start();
        }
        
        /* Set global backup state and freeze UI if one is ongoing */
        void SetBackupState(Singleton.BackupState state)
        {
            Singleton.CurrentBackupState = state;
            Singleton.BackupLevel = state == Singleton.BackupState.SINGLE_LEVEL ? levelList.SelectedItem.ToString() : "";

            bool busy = state != Singleton.BackupState.NONE;

            _isBusy = busy;
            backupProgress.Visible = busy;
            if (busy)
                backupProgress.Style = ProgressBarStyle.Marquee;

            foreach (Control control in Controls)
            {
                if (control == backupProgress)
                    continue;
                control.Enabled = !busy;
            }

            if (!busy)
            {
                saveBackup.Enabled = false;
                backupAllNow.Enabled = false;
            }
        }

        /* Create a backup of the currently selected level */
        private async void saveBackup_Click(object sender, EventArgs e)
        {
            if (!TryBeginBackupAction())
                return;

            if (backupName.Text == "")
            {
                MessageBox.Show("Please enter a backup name!");
                saveBackup.Enabled = true;
                backupAllNow.Enabled = true;
                return;
            }

            if (IsLevelActivelyBeingEdited(level.Name))
            {
                if (MessageBox.Show("This level is currently open in the script editor, would you like to save it before backing up?", "Save level?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    Singleton.Editor.SaveLevel(false);
            }

            string name = backupName.Text;
            AlienLevel backupLevel = level;

            SetBackupState(Singleton.BackupState.SINGLE_LEVEL);
            try
            {
                await Task.Run(() => backupLevel.CreateBackup(name));
                if (IsDisposed)
                    return;

                RefreshList();
                Steam.UnlockAchievement(Steam.Achievements.BACKUP_CREATED);
                MessageBox.Show("Backup successfully created!", "Backup created", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                if (!IsDisposed)
                {
                    SetBackupState(Singleton.BackupState.NONE);
                    EndBackupAction();
                }
            }
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

            if (MessageBox.Show("You are about to delete " + backupList.CheckedItems.Count + " backup" + (backupList.CheckedItems.Count > 1 ? "s" : "") + ". Are you sure?", "About to delete...", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
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
        private async void backupAllNow_Click(object sender, EventArgs e)
        {
            if (!TryBeginBackupAction())
                return;

            if (IsLevelActivelyBeingEdited())
            {
                if (MessageBox.Show("A level is currently open in the script editor, would you like to save it before backing up?", "Save level?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    Singleton.Editor.SaveLevel(false);
            }

            string selectedLevel = levelList.SelectedItem.ToString();

            SetBackupState(Singleton.BackupState.ALL_LEVELS);
            try
            {
                await Task.Run(() =>
                {
                    List<string> levels = Level.GetLevels(Singleton.PathToAI);
                    foreach (string levelName in levels)
                    {
                        AlienLevel lvl = new AlienLevel(levelName);
                        lvl.CreateBackup(lvl.Backups.Count == 0 ? "First backup" : "Automated backup across all levels");
                    }
                });
                if (IsDisposed)
                    return;

                level = new AlienLevel(selectedLevel);
                RefreshList();
                Steam.UnlockAchievement(Steam.Achievements.BACKUP_CREATED);
            }
            finally
            {
                if (!IsDisposed)
                {
                    SetBackupState(Singleton.BackupState.NONE);
                    EndBackupAction();
                }
            }
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

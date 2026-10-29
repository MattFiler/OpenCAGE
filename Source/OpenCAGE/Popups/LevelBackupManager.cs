using AlienPAK;
using CathodeLib;
using OpenCAGE.Backups;
using OpenCAGE.Popups;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            OpenCAGE.Popups.Base.BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/backup-manager/");
            Theming.ThemeManager.ApplyToForm(this);

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
            RefreshRows();
            if (level == null)
            {
                backupLabel.Text = "Create Backup";
                return;
            }
            backupLabel.Text = "Create Backup (" + level.CalculateDiff(level.Backups.Count == 0 ? null : level.Backups[level.Backups.Count - 1]) + " Changes)";
        }

        private void RefreshRows()
        {
            backupList.Items.Clear();
            if (level == null)
                return;
            for (int i = 0; i < level.Backups.Count; i++)
            {
                int changeCount = i == 0 ? level.Backups[i].GUIDs.Count : level.CalculateDiff(level.Backups[i - 1], level.Backups[i]);
                backupList.Items.Add(new ListViewItem(new string[] { level.Backups[i].Name, level.Backups[i].Date, changeCount + " Files Modified" }));
            }
        }

        /* The open Manage Backups windows showing a level's backups (UI thread) */
        internal static List<LevelBackupManager> OpenOn(string levelName)
        {
            return Application.OpenForms.OfType<LevelBackupManager>()
                .Where(o => !o.IsDisposed && o.level != null && string.Equals(o.level.Name, levelName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /* Whether it is in the middle of a backup or restore of its own */
        internal bool IsBusy => _isBusy;

        /* Re-read the level's archive after something else (an AI assistant's backup tools) wrote it, so the list shows what
           is there. The pending change count is left as it was: working it out hashes the whole level. UI thread. */
        internal void ReloadArchive()
        {
            if (_isBusy || level == null || IsDisposed)
                return;
            try { level = new AlienLevel(level.Name); }
            catch { return; } //Left showing what it read before
            RefreshRows();
        }

        /* Select a new level */
        private void levelList_SelectedIndexChanged(object sender, EventArgs e)
        {
            level = OpenLevel(levelList.SelectedItem.ToString());
            RefreshList();
        }

        /* A level's backups; null (having said why) when its list of backups can't be read */
        private AlienLevel OpenLevel(string levelName)
        {
            try
            {
                return new AlienLevel(levelName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("The list of backups for " + levelName + " can't be read, so its backups can't be shown, restored or added to here.\n\n" + ex.Message
                    + "\n\nThe file may be damaged. Moving it out of the backups folder starts a new list for this level.", "Backups unreadable", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
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
            if (level == null)
                return;
            if (!TryBeginBackupAction())
                return;

            if (backupName.Text == "")
            {
                MessageBox.Show("Please enter a backup name!");
                saveBackup.Enabled = true;
                backupAllNow.Enabled = true;
                return;
            }

            //Only asked when there is something unsaved to put in the backup
            if (IsLevelActivelyBeingEdited(level.Name) && DirtyTracker.IsDirty)
            {
                if (MessageBox.Show("This level is currently open in the script editor, would you like to save it before backing up?", "Save level?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    Singleton.Editor.SaveLevel(false, successMsg: false);
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
            catch (Exception ex)
            {
                //Out of disk space, a file held open: said here, not left to end OpenCAGE as a critical error. A backup is only
                //listed once all of it is stored, so nothing was added.
                if (!IsDisposed)
                {
                    if (AlienLevel.IsOutOfSpace(ex))
                        MessageBox.Show(ex.Message, "Not enough disk space", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    else
                        MessageBox.Show("Failed to create the backup!\n\n" + ex.Message + "\n\nNothing was added to this level's backups.", "Backup failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
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

            if (level == null)
                return;

            EditorUtils.CloseAI(null, thisInstallOnly: true);

            this.Cursor = Cursors.WaitCursor;
            bool restored;
            try
            {
                restored = level.RestoreBackup(level.Backups[backupList.SelectedItems[0].Index].ID);
            }
            catch (AlienLevel.DamagedBackupException ex)
            {
                //Refused before anything was changed: some of what it holds was not stored completely
                this.Cursor = Cursors.Default;
                MessageBox.Show(ex.Message, "Backup damaged", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Default;
                MessageBox.Show("Failed to restore backup!\n\n" + ex.Message, "Failed!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            //Only removing the copy taken first failed: the backup is in place
            if (restored || level.LastRestoreFailure == AlienLevel.RestoreFailure.CopyLeftBehind)
            {
                RefreshList();
                if (restored)
                    MessageBox.Show("Backup successfully restored!", "Restored backup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show("Backup restored, but the copy of the level taken before it could not be removed. Please delete it by hand: " + Path.GetFullPath(level.SafetyCopyFolder), "Restored backup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (IsLevelActivelyBeingEdited(level.Name))
                {
                    if (MessageBox.Show("Would you like to reload the script editor?", "Reload level?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        Singleton.Editor.LoadLevel(level.Name);
                }
            }
            else
            {
                MessageBox.Show(RestoreFailureText(), "Failed!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            this.Cursor = Cursors.Default;
        }

        /* What the last restore that failed did to the level, and why it failed */
        private string RestoreFailureText()
        {
            string why = AlienLevel.IsOutOfSpace(level.LastRestoreError)
                ? "There is not enough free space on the drive: a restore copies the level aside first, so it needs about as much free space as the level takes up."
                : "Please close anything that may be using the files within the level, and try again.";
            switch (level.LastRestoreFailure)
            {
                case AlienLevel.RestoreFailure.RolledBack:
                    return "Failed to restore backup! It failed part way, and the level was put back as it was.\n\n" + why;
                case AlienLevel.RestoreFailure.Damaged:
                    return "Failed to restore backup! It failed part way, and the level's folder could not be put back, so it may be incomplete. Its files as they were before the restore are kept in "
                        + Path.GetFullPath(level.SafetyCopyFolder) + ".\n\nTry restoring a backup again, or verify the game files.";
                default:
                    return "Failed to restore backup! Nothing in the level was changed.\n\n" + why;
            }
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
            try
            {
                for (int i = 0; i < toDelete.Count; i++)
                    level.DeleteBackup(toDelete[i].ID);
            }
            catch (Exception ex)
            {
                //The list on disk is what counts: read it again
                MessageBox.Show("Failed to delete the backup!\n\n" + ex.Message, "Delete failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                level = OpenLevel(level.Name);
            }
            RefreshList();

            this.Cursor = Cursors.Default;
        }

        /* Backup every level as they stand right now! */
        private async void backupAllNow_Click(object sender, EventArgs e)
        {
            if (!TryBeginBackupAction())
                return;

            if (IsLevelActivelyBeingEdited() && DirtyTracker.IsDirty)
            {
                if (MessageBox.Show("A level is currently open in the script editor, would you like to save it before backing up?", "Save level?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    Singleton.Editor.SaveLevel(false, successMsg: false);
            }

            string selectedLevel = levelList.SelectedItem.ToString();
            int backedUp = 0, notReached = 0;
            string outOfSpace = null;
            List<string> skipped = new List<string>();

            SetBackupState(Singleton.BackupState.ALL_LEVELS);
            try
            {
                await Task.Run(() =>
                {
                    List<string> levels = EditorUtils.GetEditableLevels();
                    for (int i = 0; i < levels.Count; i++)
                    {
                        try
                        {
                            AlienLevel lvl = new AlienLevel(levels[i]);
                            lvl.CreateBackup(lvl.Backups.Count == 0 ? "First backup" : "Automated backup across all levels");
                            backedUp++;
                        }
                        catch (Exception ex)
                        {
                            //Out of space, every level after this one would fail the same way: stop here
                            if (AlienLevel.IsOutOfSpace(ex))
                            {
                                outOfSpace = ex.Message;
                                notReached = levels.Count - i - 1;
                                break;
                            }
                            skipped.Add(levels[i] + ": " + ex.Message);
                        }
                    }
                });
                if (IsDisposed)
                    return;

                level = OpenLevel(selectedLevel);
                RefreshList();
                if (backedUp != 0)
                    Steam.UnlockAchievement(Steam.Achievements.BACKUP_CREATED);

                string done = backedUp == 0 ? "No level was backed up." : backedUp + " level" + (backedUp == 1 ? " was" : "s were") + " backed up.";
                if (outOfSpace != null)
                    MessageBox.Show(outOfSpace + "\n\n" + done + (notReached == 0 ? "" : " " + notReached + " more " + (notReached == 1 ? "was" : "were") + " not reached.")
                        + (skipped.Count == 0 ? "" : "\n\nAlso not backed up:\n" + Lines(skipped)), "Not enough disk space", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else if (skipped.Count != 0)
                    MessageBox.Show(done + " These could not be:\n\n" + Lines(skipped), "Some levels were not backed up", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                    MessageBox.Show("Failed to back up every level!\n\n" + ex.Message, "Backup failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        /* A list for a message box, one per line, kept to a size that fits on screen */
        private static string Lines(List<string> lines)
        {
            const int shown = 10;
            return string.Join("\n", lines.Take(shown)) + (lines.Count > shown ? "\n...and " + (lines.Count - shown) + " more" : "");
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

namespace CommandsEditor
{
    partial class LevelBackupManager
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LevelBackupManager));
            this.deleteSelectedBackups = new System.Windows.Forms.Button();
            this.removeSelectedBackup = new System.Windows.Forms.Button();
            this.levelList = new System.Windows.Forms.ComboBox();
            this.backupName = new System.Windows.Forms.TextBox();
            this.saveBackup = new System.Windows.Forms.Button();
            this.backupLabel = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.backupList = new System.Windows.Forms.ListView();
            this.backupNames = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.backupDates = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.backupContents = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.backupAllNow = new System.Windows.Forms.Button();
            this.revertConfigs = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // deleteSelectedBackups
            // 
            this.deleteSelectedBackups.Location = new System.Drawing.Point(710, 482);
            this.deleteSelectedBackups.Name = "deleteSelectedBackups";
            this.deleteSelectedBackups.Size = new System.Drawing.Size(180, 23);
            this.deleteSelectedBackups.TabIndex = 1;
            this.deleteSelectedBackups.Text = "Delete Checked Backups";
            this.deleteSelectedBackups.UseVisualStyleBackColor = true;
            this.deleteSelectedBackups.Click += new System.EventHandler(this.deleteSelectedBackups_Click);
            // 
            // removeSelectedBackup
            // 
            this.removeSelectedBackup.Location = new System.Drawing.Point(524, 482);
            this.removeSelectedBackup.Name = "removeSelectedBackup";
            this.removeSelectedBackup.Size = new System.Drawing.Size(180, 23);
            this.removeSelectedBackup.TabIndex = 2;
            this.removeSelectedBackup.Text = "Restore Selected Backup";
            this.removeSelectedBackup.UseVisualStyleBackColor = true;
            this.removeSelectedBackup.Click += new System.EventHandler(this.restoreSelectedBackup);
            // 
            // levelList
            // 
            this.levelList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.levelList.FormattingEnabled = true;
            this.levelList.Location = new System.Drawing.Point(12, 25);
            this.levelList.Name = "levelList";
            this.levelList.Size = new System.Drawing.Size(342, 21);
            this.levelList.TabIndex = 5;
            this.levelList.SelectedIndexChanged += new System.EventHandler(this.levelList_SelectedIndexChanged);
            // 
            // backupName
            // 
            this.backupName.Location = new System.Drawing.Point(505, 25);
            this.backupName.Name = "backupName";
            this.backupName.Size = new System.Drawing.Size(269, 20);
            this.backupName.TabIndex = 6;
            // 
            // saveBackup
            // 
            this.saveBackup.Location = new System.Drawing.Point(780, 23);
            this.saveBackup.Name = "saveBackup";
            this.saveBackup.Size = new System.Drawing.Size(111, 23);
            this.saveBackup.TabIndex = 7;
            this.saveBackup.Text = "Backup Level Now";
            this.saveBackup.UseVisualStyleBackColor = true;
            this.saveBackup.Click += new System.EventHandler(this.saveBackup_Click);
            // 
            // backupLabel
            // 
            this.backupLabel.AutoSize = true;
            this.backupLabel.Location = new System.Drawing.Point(502, 9);
            this.backupLabel.Name = "backupLabel";
            this.backupLabel.Size = new System.Drawing.Size(78, 13);
            this.backupLabel.TabIndex = 8;
            this.backupLabel.Text = "Create Backup";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 9);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(33, 13);
            this.label2.TabIndex = 9;
            this.label2.Text = "Level";
            // 
            // backupList
            // 
            this.backupList.CheckBoxes = true;
            this.backupList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.backupNames,
            this.backupDates,
            this.backupContents});
            this.backupList.FullRowSelect = true;
            this.backupList.GridLines = true;
            this.backupList.HideSelection = false;
            this.backupList.Location = new System.Drawing.Point(12, 52);
            this.backupList.Name = "backupList";
            this.backupList.Size = new System.Drawing.Size(878, 424);
            this.backupList.TabIndex = 10;
            this.backupList.UseCompatibleStateImageBehavior = false;
            this.backupList.View = System.Windows.Forms.View.Details;
            // 
            // backupNames
            // 
            this.backupNames.Text = "Name";
            this.backupNames.Width = 472;
            // 
            // backupDates
            // 
            this.backupDates.Text = "Date";
            this.backupDates.Width = 190;
            // 
            // backupContents
            // 
            this.backupContents.Text = "Details";
            this.backupContents.Width = 211;
            // 
            // backupAllNow
            // 
            this.backupAllNow.Location = new System.Drawing.Point(12, 482);
            this.backupAllNow.Name = "backupAllNow";
            this.backupAllNow.Size = new System.Drawing.Size(180, 23);
            this.backupAllNow.TabIndex = 11;
            this.backupAllNow.Text = "Backup All Levels Now";
            this.backupAllNow.UseVisualStyleBackColor = true;
            this.backupAllNow.Click += new System.EventHandler(this.backupAllNow_Click);
            // 
            // revertConfigs
            // 
            this.revertConfigs.Location = new System.Drawing.Point(198, 482);
            this.revertConfigs.Name = "revertConfigs";
            this.revertConfigs.Size = new System.Drawing.Size(180, 23);
            this.revertConfigs.TabIndex = 12;
            this.revertConfigs.Text = "Revert Configs";
            this.revertConfigs.UseVisualStyleBackColor = true;
            this.revertConfigs.Click += new System.EventHandler(this.revertConfigs_Click);
            // 
            // LevelBackupManager
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(903, 514);
            this.Controls.Add(this.revertConfigs);
            this.Controls.Add(this.backupAllNow);
            this.Controls.Add(this.backupList);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.backupLabel);
            this.Controls.Add(this.saveBackup);
            this.Controls.Add(this.backupName);
            this.Controls.Add(this.levelList);
            this.Controls.Add(this.removeSelectedBackup);
            this.Controls.Add(this.deleteSelectedBackups);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "LevelBackupManager";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Backup Manager";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button deleteSelectedBackups;
        private System.Windows.Forms.Button removeSelectedBackup;
        private System.Windows.Forms.ComboBox levelList;
        private System.Windows.Forms.TextBox backupName;
        private System.Windows.Forms.Button saveBackup;
        private System.Windows.Forms.Label backupLabel;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ListView backupList;
        private System.Windows.Forms.ColumnHeader backupNames;
        private System.Windows.Forms.ColumnHeader backupDates;
        private System.Windows.Forms.Button backupAllNow;
        private System.Windows.Forms.ColumnHeader backupContents;
        private System.Windows.Forms.Button revertConfigs;
    }
}


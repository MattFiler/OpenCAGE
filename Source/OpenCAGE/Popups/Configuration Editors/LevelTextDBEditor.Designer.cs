namespace OpenCAGE.ConfigEditors
{
    partial class LevelTextDBEditor
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
            this.components = new System.ComponentModel.Container();
            this.levelLabel = new System.Windows.Forms.Label();
            this.levelList = new System.Windows.Forms.ListBox();
            this.sharedGroup = new System.Windows.Forms.GroupBox();
            this.sharedDbList = new System.Windows.Forms.CheckedListBox();
            this.editSharedDb = new System.Windows.Forms.Button();
            this.missingLabel = new System.Windows.Forms.Label();
            this.localGroup = new System.Windows.Forms.GroupBox();
            this.localDbList = new System.Windows.Forms.CheckedListBox();
            this.newLocalDb = new System.Windows.Forms.Button();
            this.editLocalDb = new System.Windows.Forms.Button();
            this.localHint = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.sharedGroup.SuspendLayout();
            this.localGroup.SuspendLayout();
            this.SuspendLayout();
            //
            // levelLabel
            //
            this.levelLabel.AutoSize = true;
            this.levelLabel.Location = new System.Drawing.Point(12, 9);
            this.levelLabel.Name = "levelLabel";
            this.levelLabel.Size = new System.Drawing.Size(36, 13);
            this.levelLabel.TabIndex = 0;
            this.levelLabel.Text = "Level:";
            //
            // levelList
            //
            this.levelList.FormattingEnabled = true;
            this.levelList.IntegralHeight = false;
            this.levelList.Location = new System.Drawing.Point(15, 28);
            this.levelList.Name = "levelList";
            this.levelList.Size = new System.Drawing.Size(240, 500);
            this.levelList.TabIndex = 1;
            this.toolTip1.SetToolTip(this.levelList, "The config keys a level by its folder name, so two levels with the same folder na" +
        "me would share an entry.");
            this.levelList.SelectedIndexChanged += new System.EventHandler(this.levelList_SelectedIndexChanged);
            //
            // sharedGroup
            //
            this.sharedGroup.Controls.Add(this.sharedDbList);
            this.sharedGroup.Controls.Add(this.editSharedDb);
            this.sharedGroup.Controls.Add(this.missingLabel);
            this.sharedGroup.Location = new System.Drawing.Point(270, 12);
            this.sharedGroup.Name = "sharedGroup";
            this.sharedGroup.Size = new System.Drawing.Size(380, 516);
            this.sharedGroup.TabIndex = 2;
            this.sharedGroup.TabStop = false;
            this.sharedGroup.Text = "Shared databases (DATA/TEXT)";
            //
            // sharedDbList
            //
            this.sharedDbList.CheckOnClick = true;
            this.sharedDbList.FormattingEnabled = true;
            this.sharedDbList.IntegralHeight = false;
            this.sharedDbList.Location = new System.Drawing.Point(12, 22);
            this.sharedDbList.Name = "sharedDbList";
            this.sharedDbList.Size = new System.Drawing.Size(356, 410);
            this.sharedDbList.TabIndex = 0;
            this.toolTip1.SetToolTip(this.sharedDbList, "Databases this level loads out of DATA/TEXT, written to DATA/LEVEL_TEXT_DATABASES" +
        ".XML.");
            this.sharedDbList.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.sharedDbList_ItemCheck);
            //
            // editSharedDb
            //
            this.editSharedDb.Location = new System.Drawing.Point(12, 440);
            this.editSharedDb.Name = "editSharedDb";
            this.editSharedDb.Size = new System.Drawing.Size(220, 26);
            this.editSharedDb.TabIndex = 1;
            this.editSharedDb.Text = "Edit Selected Database Contents";
            this.editSharedDb.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.editSharedDb, "Open the localisation editor on the selected database, in every language.");
            this.editSharedDb.Click += new System.EventHandler(this.editSharedDb_Click);
            //
            // missingLabel
            //
            this.missingLabel.Location = new System.Drawing.Point(12, 476);
            this.missingLabel.Name = "missingLabel";
            this.missingLabel.Size = new System.Drawing.Size(356, 34);
            this.missingLabel.TabIndex = 2;
            //
            // localGroup
            //
            this.localGroup.Controls.Add(this.localDbList);
            this.localGroup.Controls.Add(this.newLocalDb);
            this.localGroup.Controls.Add(this.editLocalDb);
            this.localGroup.Controls.Add(this.localHint);
            this.localGroup.Location = new System.Drawing.Point(660, 12);
            this.localGroup.Name = "localGroup";
            this.localGroup.Size = new System.Drawing.Size(380, 516);
            this.localGroup.TabIndex = 3;
            this.localGroup.TabStop = false;
            this.localGroup.Text = "Databases inside the level";
            //
            // localDbList
            //
            this.localDbList.CheckOnClick = true;
            this.localDbList.FormattingEnabled = true;
            this.localDbList.IntegralHeight = false;
            this.localDbList.Location = new System.Drawing.Point(12, 22);
            this.localDbList.Name = "localDbList";
            this.localDbList.Size = new System.Drawing.Size(356, 410);
            this.localDbList.TabIndex = 0;
            this.toolTip1.SetToolTip(this.localDbList, "Databases shipped in the level\'s own TEXT folder, written to its TEXT/TEXT_DB_LIS" +
        "T.TXT. These add to the shared ones rather than replacing them.");
            this.localDbList.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.localDbList_ItemCheck);
            //
            // newLocalDb
            //
            this.newLocalDb.Location = new System.Drawing.Point(12, 440);
            this.newLocalDb.Name = "newLocalDb";
            this.newLocalDb.Size = new System.Drawing.Size(130, 26);
            this.newLocalDb.TabIndex = 1;
            this.newLocalDb.Text = "New Database...";
            this.newLocalDb.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.newLocalDb, "Create an empty database inside this level, in every language, and list it.");
            this.newLocalDb.Click += new System.EventHandler(this.newLocalDb_Click);
            //
            // editLocalDb
            //
            this.editLocalDb.Location = new System.Drawing.Point(148, 440);
            this.editLocalDb.Name = "editLocalDb";
            this.editLocalDb.Size = new System.Drawing.Size(220, 26);
            this.editLocalDb.TabIndex = 2;
            this.editLocalDb.Text = "Edit Selected Database Contents";
            this.editLocalDb.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.editLocalDb, "Open the localisation editor on the selected database, in every language.");
            this.editLocalDb.Click += new System.EventHandler(this.editLocalDb_Click);
            //
            // localHint
            //
            this.localHint.Location = new System.Drawing.Point(12, 476);
            this.localHint.Name = "localHint";
            this.localHint.Size = new System.Drawing.Size(356, 34);
            this.localHint.TabIndex = 3;
            //
            // LevelTextDBEditor
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1054, 541);
            this.Controls.Add(this.localGroup);
            this.Controls.Add(this.sharedGroup);
            this.Controls.Add(this.levelList);
            this.Controls.Add(this.levelLabel);
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "LevelTextDBEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Level Text Database Editor";
            this.sharedGroup.ResumeLayout(false);
            this.localGroup.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label levelLabel;
        private System.Windows.Forms.ListBox levelList;
        private System.Windows.Forms.GroupBox sharedGroup;
        private System.Windows.Forms.CheckedListBox sharedDbList;
        private System.Windows.Forms.Button editSharedDb;
        private System.Windows.Forms.Label missingLabel;
        private System.Windows.Forms.GroupBox localGroup;
        private System.Windows.Forms.CheckedListBox localDbList;
        private System.Windows.Forms.Button newLocalDb;
        private System.Windows.Forms.Button editLocalDb;
        private System.Windows.Forms.Label localHint;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}

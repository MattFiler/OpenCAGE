namespace OpenCAGE
{
    partial class Settings
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Settings));
            this.useStaging = new System.Windows.Forms.CheckBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.showPlatform = new System.Windows.Forms.CheckBox();
            this.resetAll = new System.Windows.Forms.Button();
            this.assetFileLockWarning = new System.Windows.Forms.CheckBox();
            this.saveConfig = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // useStaging
            // 
            this.useStaging.AutoSize = true;
            this.useStaging.Location = new System.Drawing.Point(8, 14);
            this.useStaging.Name = "useStaging";
            this.useStaging.Size = new System.Drawing.Size(203, 17);
            this.useStaging.TabIndex = 0;
            this.useStaging.Text = "Receive updates from staging branch";
            this.toolTip1.SetToolTip(this.useStaging, "When checked, OpenCAGE will sync updates with the latest published version revisi" +
        "on on \"staging\" rather than \"master\". These builds may be unstable compared to t" +
        "he \"master\" releases.");
            this.useStaging.UseVisualStyleBackColor = true;
            // 
            // showPlatform
            // 
            this.showPlatform.AutoSize = true;
            this.showPlatform.Location = new System.Drawing.Point(8, 37);
            this.showPlatform.Name = "showPlatform";
            this.showPlatform.Size = new System.Drawing.Size(190, 17);
            this.showPlatform.TabIndex = 1;
            this.showPlatform.Text = "Show game platform in script editor";
            this.toolTip1.SetToolTip(this.showPlatform, "When checked, the Commands Editor will show the current game platform in its titl" +
        "e bar. Useful when editing scripts across platforms!");
            this.showPlatform.UseVisualStyleBackColor = true;
            // 
            // resetAll
            // 
            this.resetAll.Location = new System.Drawing.Point(5, 125);
            this.resetAll.Name = "resetAll";
            this.resetAll.Size = new System.Drawing.Size(217, 23);
            this.resetAll.TabIndex = 99;
            this.resetAll.Text = "Verify OpenCAGE Tools";
            this.toolTip1.SetToolTip(this.resetAll, "This will re-download all OpenCAGE components - useful if you have been encounter" +
        "ing issues!");
            this.resetAll.UseVisualStyleBackColor = true;
            this.resetAll.Click += new System.EventHandler(this.resetAll_Click);
            // 
            // assetFileLockWarning
            // 
            this.assetFileLockWarning.AutoSize = true;
            this.assetFileLockWarning.Location = new System.Drawing.Point(8, 59);
            this.assetFileLockWarning.Name = "assetFileLockWarning";
            this.assetFileLockWarning.Size = new System.Drawing.Size(184, 17);
            this.assetFileLockWarning.TabIndex = 2;
            this.assetFileLockWarning.Text = "Hide asset editor file lock warning";
            this.toolTip1.SetToolTip(this.assetFileLockWarning, "When checked, the Asset Editor will no longer show the startup warning about prev" +
        "enting file locks.");
            this.assetFileLockWarning.UseVisualStyleBackColor = true;
            // 
            // saveConfig
            // 
            this.saveConfig.Location = new System.Drawing.Point(6, 82);
            this.saveConfig.Name = "saveConfig";
            this.saveConfig.Size = new System.Drawing.Size(205, 31);
            this.saveConfig.TabIndex = 3;
            this.saveConfig.Text = "Apply Settings";
            this.saveConfig.UseVisualStyleBackColor = true;
            this.saveConfig.Click += new System.EventHandler(this.saveConfig_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.assetFileLockWarning);
            this.groupBox1.Controls.Add(this.useStaging);
            this.groupBox1.Controls.Add(this.saveConfig);
            this.groupBox1.Controls.Add(this.showPlatform);
            this.groupBox1.Location = new System.Drawing.Point(5, 0);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(217, 119);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            // 
            // Settings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(228, 154);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.resetAll);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "Settings";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "OpenCAGE Settings";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.CheckBox useStaging;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Button saveConfig;
        private System.Windows.Forms.CheckBox showPlatform;
        private System.Windows.Forms.Button resetAll;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox assetFileLockWarning;
    }
}
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
            this.saveConfig = new System.Windows.Forms.Button();
            this.showPlatform = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // useStaging
            // 
            this.useStaging.AutoSize = true;
            this.useStaging.Location = new System.Drawing.Point(12, 12);
            this.useStaging.Name = "useStaging";
            this.useStaging.Size = new System.Drawing.Size(203, 17);
            this.useStaging.TabIndex = 0;
            this.useStaging.Text = "Recieve updates from staging branch";
            this.toolTip1.SetToolTip(this.useStaging, "When checked, OpenCAGE will sync updates with the latest published version revisi" +
        "on on \"staging\" rather than \"master\". These builds may be unstable compared to t" +
        "he \"master\" releases.");
            this.useStaging.UseVisualStyleBackColor = true;
            // 
            // saveConfig
            // 
            this.saveConfig.Location = new System.Drawing.Point(140, 58);
            this.saveConfig.Name = "saveConfig";
            this.saveConfig.Size = new System.Drawing.Size(75, 23);
            this.saveConfig.TabIndex = 1;
            this.saveConfig.Text = "Apply";
            this.saveConfig.UseVisualStyleBackColor = true;
            this.saveConfig.Click += new System.EventHandler(this.saveConfig_Click);
            // 
            // showPlatform
            // 
            this.showPlatform.AutoSize = true;
            this.showPlatform.Location = new System.Drawing.Point(12, 35);
            this.showPlatform.Name = "showPlatform";
            this.showPlatform.Size = new System.Drawing.Size(190, 17);
            this.showPlatform.TabIndex = 2;
            this.showPlatform.Text = "Show game platform in script editor";
            this.toolTip1.SetToolTip(this.showPlatform, "When checked, the CATHODE script editor will show the current game platform in it" +
        "s title bar. Useful when editing scripts across platforms!");
            this.showPlatform.UseVisualStyleBackColor = true;
            // 
            // Settings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(227, 91);
            this.Controls.Add(this.showPlatform);
            this.Controls.Add(this.saveConfig);
            this.Controls.Add(this.useStaging);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Settings";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "OpenCAGE Settings";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox useStaging;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Button saveConfig;
        private System.Windows.Forms.CheckBox showPlatform;
    }
}
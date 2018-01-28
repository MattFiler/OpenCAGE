namespace PackagingTool
{
    partial class BehaviourPackerSettings
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BehaviourPackerSettings));
            this.setting_OpenOutputFolder = new System.Windows.Forms.CheckBox();
            this.setting_RunGame = new System.Windows.Forms.CheckBox();
            this.saveSettings = new System.Windows.Forms.Button();
            this.settings_showMessageBox = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // setting_OpenOutputFolder
            // 
            this.setting_OpenOutputFolder.AutoSize = true;
            this.setting_OpenOutputFolder.Location = new System.Drawing.Point(12, 12);
            this.setting_OpenOutputFolder.Name = "setting_OpenOutputFolder";
            this.setting_OpenOutputFolder.Size = new System.Drawing.Size(170, 17);
            this.setting_OpenOutputFolder.TabIndex = 1;
            this.setting_OpenOutputFolder.Text = "Open output folder after export";
            this.setting_OpenOutputFolder.UseVisualStyleBackColor = true;
            this.setting_OpenOutputFolder.CheckedChanged += new System.EventHandler(this.setting_OpenOutputFolder_CheckedChanged);
            // 
            // setting_RunGame
            // 
            this.setting_RunGame.AutoSize = true;
            this.setting_RunGame.Location = new System.Drawing.Point(12, 35);
            this.setting_RunGame.Name = "setting_RunGame";
            this.setting_RunGame.Size = new System.Drawing.Size(121, 17);
            this.setting_RunGame.TabIndex = 2;
            this.setting_RunGame.Text = "Run game on import";
            this.setting_RunGame.UseVisualStyleBackColor = true;
            this.setting_RunGame.CheckedChanged += new System.EventHandler(this.setting_RunGame_CheckedChanged);
            // 
            // saveSettings
            // 
            this.saveSettings.Location = new System.Drawing.Point(11, 81);
            this.saveSettings.Name = "saveSettings";
            this.saveSettings.Size = new System.Drawing.Size(170, 23);
            this.saveSettings.TabIndex = 3;
            this.saveSettings.Text = "Save Settings";
            this.saveSettings.UseVisualStyleBackColor = true;
            this.saveSettings.Click += new System.EventHandler(this.saveSettings_Click);
            // 
            // settings_showMessageBox
            // 
            this.settings_showMessageBox.AutoSize = true;
            this.settings_showMessageBox.Location = new System.Drawing.Point(12, 58);
            this.settings_showMessageBox.Name = "settings_showMessageBox";
            this.settings_showMessageBox.Size = new System.Drawing.Size(145, 17);
            this.settings_showMessageBox.TabIndex = 4;
            this.settings_showMessageBox.Text = "Show prompts for actions";
            this.settings_showMessageBox.UseVisualStyleBackColor = true;
            this.settings_showMessageBox.CheckedChanged += new System.EventHandler(this.settings_showMessageBox_CheckedChanged);
            // 
            // Form2
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(193, 113);
            this.Controls.Add(this.settings_showMessageBox);
            this.Controls.Add(this.saveSettings);
            this.Controls.Add(this.setting_RunGame);
            this.Controls.Add(this.setting_OpenOutputFolder);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Form2";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "PackagingTool Settings";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox setting_OpenOutputFolder;
        private System.Windows.Forms.CheckBox setting_RunGame;
        private System.Windows.Forms.Button saveSettings;
        private System.Windows.Forms.CheckBox settings_showMessageBox;
    }
}
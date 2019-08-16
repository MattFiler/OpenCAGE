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
            this.setting_RunGame = new System.Windows.Forms.CheckBox();
            this.settings_showMessageBox = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // setting_RunGame
            // 
            this.setting_RunGame.AutoSize = true;
            this.setting_RunGame.Location = new System.Drawing.Point(11, 12);
            this.setting_RunGame.Name = "setting_RunGame";
            this.setting_RunGame.Size = new System.Drawing.Size(144, 17);
            this.setting_RunGame.TabIndex = 2;
            this.setting_RunGame.Text = "Run game after importing";
            this.setting_RunGame.UseVisualStyleBackColor = true;
            this.setting_RunGame.CheckedChanged += new System.EventHandler(this.setting_RunGame_CheckedChanged);
            // 
            // settings_showMessageBox
            // 
            this.settings_showMessageBox.AutoSize = true;
            this.settings_showMessageBox.Location = new System.Drawing.Point(11, 35);
            this.settings_showMessageBox.Name = "settings_showMessageBox";
            this.settings_showMessageBox.Size = new System.Drawing.Size(144, 17);
            this.settings_showMessageBox.TabIndex = 4;
            this.settings_showMessageBox.Text = "Show import confirmation";
            this.settings_showMessageBox.UseVisualStyleBackColor = true;
            this.settings_showMessageBox.CheckedChanged += new System.EventHandler(this.settings_showMessageBox_CheckedChanged);
            // 
            // BehaviourPackerSettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(193, 63);
            this.Controls.Add(this.settings_showMessageBox);
            this.Controls.Add(this.setting_RunGame);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "BehaviourPackerSettings";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Settings";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.CheckBox setting_RunGame;
        private System.Windows.Forms.CheckBox settings_showMessageBox;
    }
}
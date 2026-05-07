namespace OpenCAGE.Popups.Configuration_Editors.User_Controls
{
    partial class ViewconeDifficultySet
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tabControlVC = new System.Windows.Forms.TabControl();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.tabPage5 = new System.Windows.Forms.TabPage();
            this.tabPage6 = new System.Windows.Forms.TabPage();
            this.tabPage7 = new System.Windows.Forms.TabPage();
            this.vc_Focused = new ConfigEditors.ViewconeDifficulty();
            this.vc_Close = new ConfigEditors.ViewconeDifficulty();
            this.vc_Normal = new ConfigEditors.ViewconeDifficulty();
            this.vc_Peripheral = new ConfigEditors.ViewconeDifficulty();
            this.tabControlVC.SuspendLayout();
            this.tabPage4.SuspendLayout();
            this.tabPage5.SuspendLayout();
            this.tabPage6.SuspendLayout();
            this.tabPage7.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControlVC
            // 
            this.tabControlVC.Controls.Add(this.tabPage4);
            this.tabControlVC.Controls.Add(this.tabPage5);
            this.tabControlVC.Controls.Add(this.tabPage6);
            this.tabControlVC.Controls.Add(this.tabPage7);
            this.tabControlVC.Location = new System.Drawing.Point(3, 3);
            this.tabControlVC.Name = "tabControlVC";
            this.tabControlVC.SelectedIndex = 0;
            this.tabControlVC.Size = new System.Drawing.Size(428, 82);
            this.tabControlVC.TabIndex = 421;
            // 
            // tabPage4
            // 
            this.tabPage4.Controls.Add(this.vc_Focused);
            this.tabPage4.Location = new System.Drawing.Point(4, 22);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage4.Size = new System.Drawing.Size(420, 56);
            this.tabPage4.TabIndex = 0;
            this.tabPage4.Text = "Focused";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // tabPage5
            // 
            this.tabPage5.Controls.Add(this.vc_Close);
            this.tabPage5.Location = new System.Drawing.Point(4, 22);
            this.tabPage5.Name = "tabPage5";
            this.tabPage5.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage5.Size = new System.Drawing.Size(447, 85);
            this.tabPage5.TabIndex = 1;
            this.tabPage5.Text = "Close";
            this.tabPage5.UseVisualStyleBackColor = true;
            // 
            // tabPage6
            // 
            this.tabPage6.Controls.Add(this.vc_Normal);
            this.tabPage6.Location = new System.Drawing.Point(4, 22);
            this.tabPage6.Name = "tabPage6";
            this.tabPage6.Size = new System.Drawing.Size(447, 85);
            this.tabPage6.TabIndex = 2;
            this.tabPage6.Text = "Normal";
            this.tabPage6.UseVisualStyleBackColor = true;
            // 
            // tabPage7
            // 
            this.tabPage7.Controls.Add(this.vc_Peripheral);
            this.tabPage7.Location = new System.Drawing.Point(4, 22);
            this.tabPage7.Name = "tabPage7";
            this.tabPage7.Size = new System.Drawing.Size(447, 85);
            this.tabPage7.TabIndex = 3;
            this.tabPage7.Text = "Peripheral";
            this.tabPage7.UseVisualStyleBackColor = true;
            // 
            // vc_Focused
            // 
            this.vc_Focused.Location = new System.Drawing.Point(6, 6);
            this.vc_Focused.Name = "vc_Focused";
            this.vc_Focused.Size = new System.Drawing.Size(414, 48);
            this.vc_Focused.TabIndex = 0;
            // 
            // vc_Close
            // 
            this.vc_Close.Location = new System.Drawing.Point(6, 6);
            this.vc_Close.Name = "vc_Close";
            this.vc_Close.Size = new System.Drawing.Size(437, 74);
            this.vc_Close.TabIndex = 1;
            // 
            // vc_Normal
            // 
            this.vc_Normal.Location = new System.Drawing.Point(6, 6);
            this.vc_Normal.Name = "vc_Normal";
            this.vc_Normal.Size = new System.Drawing.Size(437, 74);
            this.vc_Normal.TabIndex = 1;
            // 
            // vc_Peripheral
            // 
            this.vc_Peripheral.Location = new System.Drawing.Point(6, 6);
            this.vc_Peripheral.Name = "vc_Peripheral";
            this.vc_Peripheral.Size = new System.Drawing.Size(437, 74);
            this.vc_Peripheral.TabIndex = 1;
            // 
            // ViewconeDifficultySet
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tabControlVC);
            this.Name = "ViewconeDifficultySet";
            this.Size = new System.Drawing.Size(435, 89);
            this.tabControlVC.ResumeLayout(false);
            this.tabPage4.ResumeLayout(false);
            this.tabPage5.ResumeLayout(false);
            this.tabPage6.ResumeLayout(false);
            this.tabPage7.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControlVC;
        private System.Windows.Forms.TabPage tabPage4;
        private ConfigEditors.ViewconeDifficulty vc_Focused;
        private System.Windows.Forms.TabPage tabPage5;
        private ConfigEditors.ViewconeDifficulty vc_Close;
        private System.Windows.Forms.TabPage tabPage6;
        private ConfigEditors.ViewconeDifficulty vc_Normal;
        private System.Windows.Forms.TabPage tabPage7;
        private ConfigEditors.ViewconeDifficulty vc_Peripheral;
    }
}

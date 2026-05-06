namespace OpenCAGE.ConfigEditors
{
    partial class SenseEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SenseEditor));
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.set1Normal = new ConfigEditors.SenseSet();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.set1Heightened = new ConfigEditors.SenseSet();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.set2 = new ConfigEditors.SenseSet();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.set3 = new ConfigEditors.SenseSet();
            this.characters = new System.Windows.Forms.ComboBox();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.tabPage4.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.Location = new System.Drawing.Point(11, 39);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(460, 419);
            this.tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.set1Normal);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(452, 393);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Set 1 Normal";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // set1Normal
            // 
            this.set1Normal.Location = new System.Drawing.Point(6, 4);
            this.set1Normal.Name = "set1Normal";
            this.set1Normal.Size = new System.Drawing.Size(441, 384);
            this.set1Normal.TabIndex = 2;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.set1Heightened);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(452, 393);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Set 1 Heightened";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // set1Heightened
            // 
            this.set1Heightened.Location = new System.Drawing.Point(6, 4);
            this.set1Heightened.Name = "set1Heightened";
            this.set1Heightened.Size = new System.Drawing.Size(441, 384);
            this.set1Heightened.TabIndex = 2;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.set2);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Size = new System.Drawing.Size(452, 393);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Set 2";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // set2
            // 
            this.set2.Location = new System.Drawing.Point(6, 4);
            this.set2.Name = "set2";
            this.set2.Size = new System.Drawing.Size(441, 384);
            this.set2.TabIndex = 2;
            // 
            // tabPage4
            // 
            this.tabPage4.Controls.Add(this.set3);
            this.tabPage4.Location = new System.Drawing.Point(4, 22);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Size = new System.Drawing.Size(452, 393);
            this.tabPage4.TabIndex = 3;
            this.tabPage4.Text = "Set 3";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // set3
            // 
            this.set3.Location = new System.Drawing.Point(6, 4);
            this.set3.Name = "set3";
            this.set3.Size = new System.Drawing.Size(441, 384);
            this.set3.TabIndex = 2;
            // 
            // characters
            // 
            this.characters.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.characters.FormattingEnabled = true;
            this.characters.Location = new System.Drawing.Point(12, 12);
            this.characters.Name = "characters";
            this.characters.Size = new System.Drawing.Size(456, 21);
            this.characters.TabIndex = 1;
            this.characters.SelectedIndexChanged += new System.EventHandler(this.characters_SelectedIndexChanged);
            // 
            // SenseEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(481, 466);
            this.Controls.Add(this.characters);
            this.Controls.Add(this.tabControl1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "SenseEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Sense Editor";
            this.Load += new System.EventHandler(this.SenseEditor_Load);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
            this.tabPage4.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private ConfigEditors.SenseSet set1Normal;
        private ConfigEditors.SenseSet set1Heightened;
        private System.Windows.Forms.TabPage tabPage3;
        private ConfigEditors.SenseSet set2;
        private System.Windows.Forms.TabPage tabPage4;
        private ConfigEditors.SenseSet set3;
        private System.Windows.Forms.ComboBox characters;
    }
}

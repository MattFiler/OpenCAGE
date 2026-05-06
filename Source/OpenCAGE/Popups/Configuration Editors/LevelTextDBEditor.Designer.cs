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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LevelTextDBEditor));
            this.SaveBtn = new System.Windows.Forms.Button();
            this.levelList = new System.Windows.Forms.ListBox();
            this.textDbList = new System.Windows.Forms.CheckedListBox();
            this.useLocalTextDBs = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // SaveBtn
            // 
            this.SaveBtn.Location = new System.Drawing.Point(689, 591);
            this.SaveBtn.Name = "SaveBtn";
            this.SaveBtn.Size = new System.Drawing.Size(198, 61);
            this.SaveBtn.TabIndex = 0;
            this.SaveBtn.Text = "Save";
            this.SaveBtn.UseVisualStyleBackColor = true;
            this.SaveBtn.Click += new System.EventHandler(this.SaveBtn_Click);
            // 
            // levelList
            // 
            this.levelList.FormattingEnabled = true;
            this.levelList.Location = new System.Drawing.Point(58, 48);
            this.levelList.Name = "levelList";
            this.levelList.Size = new System.Drawing.Size(344, 472);
            this.levelList.TabIndex = 1;
            this.levelList.SelectedIndexChanged += new System.EventHandler(this.levelList_SelectedIndexChanged);
            // 
            // textDbList
            // 
            this.textDbList.FormattingEnabled = true;
            this.textDbList.Location = new System.Drawing.Point(435, 49);
            this.textDbList.Name = "textDbList";
            this.textDbList.Size = new System.Drawing.Size(348, 469);
            this.textDbList.TabIndex = 2;
            // 
            // useLocalTextDBs
            // 
            this.useLocalTextDBs.AutoSize = true;
            this.useLocalTextDBs.Location = new System.Drawing.Point(435, 26);
            this.useLocalTextDBs.Name = "useLocalTextDBs";
            this.useLocalTextDBs.Size = new System.Drawing.Size(154, 17);
            this.useLocalTextDBs.TabIndex = 3;
            this.useLocalTextDBs.Text = "Use Text DBs local to level";
            this.useLocalTextDBs.UseVisualStyleBackColor = true;
            // 
            // LevelTextDBEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(994, 731);
            this.Controls.Add(this.useLocalTextDBs);
            this.Controls.Add(this.textDbList);
            this.Controls.Add(this.levelList);
            this.Controls.Add(this.SaveBtn);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "LevelTextDBEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Level Text Database Editor";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button SaveBtn;
        private System.Windows.Forms.ListBox levelList;
        private System.Windows.Forms.CheckedListBox textDbList;
        private System.Windows.Forms.CheckBox useLocalTextDBs;
    }
}

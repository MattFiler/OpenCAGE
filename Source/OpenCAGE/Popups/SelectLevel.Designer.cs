namespace OpenCAGE.Popups
{
    partial class SelectLevel
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SelectLevel));
            this.load_commands_pak = new System.Windows.Forms.Button();
            this.env_list = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // load_commands_pak
            // 
            this.load_commands_pak.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.load_commands_pak.Location = new System.Drawing.Point(302, 11);
            this.load_commands_pak.Name = "load_commands_pak";
            this.load_commands_pak.Size = new System.Drawing.Size(86, 23);
            this.load_commands_pak.TabIndex = 174;
            this.load_commands_pak.Text = "Load";
            this.load_commands_pak.UseVisualStyleBackColor = true;
            this.load_commands_pak.Click += new System.EventHandler(this.load_commands_pak_Click);
            // 
            // env_list
            // 
            this.env_list.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.env_list.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.env_list.FormattingEnabled = true;
            this.env_list.Location = new System.Drawing.Point(12, 12);
            this.env_list.Name = "env_list";
            this.env_list.Size = new System.Drawing.Size(284, 21);
            this.env_list.TabIndex = 175;
            this.env_list.SelectedIndexChanged += new System.EventHandler(this.env_list_SelectedIndexChanged);
            // 
            // SelectLevel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(400, 43);
            this.Controls.Add(this.load_commands_pak);
            this.Controls.Add(this.env_list);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.MinimumSize = new System.Drawing.Size(416, 82);
            this.Name = "SelectLevel";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Select Level";
            this.TopMost = true;
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button load_commands_pak;
        private System.Windows.Forms.ComboBox env_list;
    }
}

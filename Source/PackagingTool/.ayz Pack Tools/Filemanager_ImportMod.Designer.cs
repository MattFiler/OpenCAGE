namespace Alien_Isolation_Mod_Tools
{
    partial class Filemanager_ImportMod
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Filemanager_ImportMod));
            this.HeaderText = new System.Windows.Forms.Label();
            this.HeaderImage = new System.Windows.Forms.PictureBox();
            this.InstalledMods = new System.Windows.Forms.ListBox();
            this.SelectMod = new System.Windows.Forms.Button();
            this.Title1 = new System.Windows.Forms.Label();
            this.CloseButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.SELECTED_MOD_TITLE = new System.Windows.Forms.Label();
            this.MOD_PREVIEW = new System.Windows.Forms.PictureBox();
            this.SELECTED_MOD_DESCRIPTION = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.HeaderImage)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.MOD_PREVIEW)).BeginInit();
            this.SuspendLayout();
            // 
            // HeaderText
            // 
            this.HeaderText.AutoSize = true;
            this.HeaderText.BackColor = System.Drawing.Color.Transparent;
            this.HeaderText.Font = new System.Drawing.Font("Jixellation", 80.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.HeaderText.ForeColor = System.Drawing.Color.White;
            this.HeaderText.Location = new System.Drawing.Point(21, 0);
            this.HeaderText.Name = "HeaderText";
            this.HeaderText.Size = new System.Drawing.Size(317, 280);
            this.HeaderText.TabIndex = 17;
            this.HeaderText.Text = "LOAD\r\nMOD";
            this.HeaderText.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // HeaderImage
            // 
            this.HeaderImage.BackgroundImage = global::Alien_Isolation_Mod_Tools.Properties.Resources.CONNOR;
            this.HeaderImage.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.HeaderImage.InitialImage = global::Alien_Isolation_Mod_Tools.Properties.Resources.CONNOR;
            this.HeaderImage.Location = new System.Drawing.Point(-15, -2);
            this.HeaderImage.Name = "HeaderImage";
            this.HeaderImage.Size = new System.Drawing.Size(1223, 498);
            this.HeaderImage.TabIndex = 16;
            this.HeaderImage.TabStop = false;
            // 
            // InstalledMods
            // 
            this.InstalledMods.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.InstalledMods.FormattingEnabled = true;
            this.InstalledMods.ItemHeight = 25;
            this.InstalledMods.Location = new System.Drawing.Point(222, 550);
            this.InstalledMods.Name = "InstalledMods";
            this.InstalledMods.ScrollAlwaysVisible = true;
            this.InstalledMods.Size = new System.Drawing.Size(402, 304);
            this.InstalledMods.TabIndex = 26;
            this.InstalledMods.SelectedIndexChanged += new System.EventHandler(this.InstalledMods_SelectedIndexChanged);
            // 
            // SelectMod
            // 
            this.SelectMod.Enabled = false;
            this.SelectMod.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SelectMod.Location = new System.Drawing.Point(630, 781);
            this.SelectMod.Name = "SelectMod";
            this.SelectMod.Size = new System.Drawing.Size(289, 73);
            this.SelectMod.TabIndex = 27;
            this.SelectMod.Text = "Install Mod";
            this.SelectMod.UseVisualStyleBackColor = true;
            this.SelectMod.Click += new System.EventHandler(this.SelectMod_Click);
            // 
            // Title1
            // 
            this.Title1.AutoSize = true;
            this.Title1.Font = new System.Drawing.Font("Isolation", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Title1.Location = new System.Drawing.Point(216, 497);
            this.Title1.Name = "Title1";
            this.Title1.Size = new System.Drawing.Size(232, 33);
            this.Title1.TabIndex = 28;
            this.Title1.Text = "MOD_LIST_TITLE";
            // 
            // CloseButton
            // 
            this.CloseButton.Location = new System.Drawing.Point(1115, 799);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.Size = new System.Drawing.Size(64, 56);
            this.CloseButton.TabIndex = 29;
            this.CloseButton.Text = "CLOSE";
            this.CloseButton.UseVisualStyleBackColor = true;
            this.CloseButton.Click += new System.EventHandler(this.CloseButton_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(219, 530);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(405, 13);
            this.label1.TabIndex = 30;
            this.label1.Text = "Expecting to see more? Make sure to follow the mod download instructions correctl" +
    "y!";
            // 
            // SELECTED_MOD_TITLE
            // 
            this.SELECTED_MOD_TITLE.AutoSize = true;
            this.SELECTED_MOD_TITLE.Font = new System.Drawing.Font("Isolation", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SELECTED_MOD_TITLE.Location = new System.Drawing.Point(626, 682);
            this.SELECTED_MOD_TITLE.Name = "SELECTED_MOD_TITLE";
            this.SELECTED_MOD_TITLE.Size = new System.Drawing.Size(240, 25);
            this.SELECTED_MOD_TITLE.TabIndex = 31;
            this.SELECTED_MOD_TITLE.Text = "SELECTED_MOD_TITLE";
            // 
            // MOD_PREVIEW
            // 
            this.MOD_PREVIEW.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.MOD_PREVIEW.Location = new System.Drawing.Point(630, 550);
            this.MOD_PREVIEW.Name = "MOD_PREVIEW";
            this.MOD_PREVIEW.Size = new System.Drawing.Size(289, 128);
            this.MOD_PREVIEW.TabIndex = 32;
            this.MOD_PREVIEW.TabStop = false;
            // 
            // SELECTED_MOD_DESCRIPTION
            // 
            this.SELECTED_MOD_DESCRIPTION.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.SELECTED_MOD_DESCRIPTION.Font = new System.Drawing.Font("Jixellation", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SELECTED_MOD_DESCRIPTION.Location = new System.Drawing.Point(630, 710);
            this.SELECTED_MOD_DESCRIPTION.Multiline = true;
            this.SELECTED_MOD_DESCRIPTION.Name = "SELECTED_MOD_DESCRIPTION";
            this.SELECTED_MOD_DESCRIPTION.ReadOnly = true;
            this.SELECTED_MOD_DESCRIPTION.Size = new System.Drawing.Size(289, 68);
            this.SELECTED_MOD_DESCRIPTION.TabIndex = 33;
            this.SELECTED_MOD_DESCRIPTION.Text = "SELECTED_MOD_DESCRIPTION";
            // 
            // Filemanager_ImportMod
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1191, 867);
            this.Controls.Add(this.SELECTED_MOD_DESCRIPTION);
            this.Controls.Add(this.MOD_PREVIEW);
            this.Controls.Add(this.SELECTED_MOD_TITLE);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.CloseButton);
            this.Controls.Add(this.Title1);
            this.Controls.Add(this.SelectMod);
            this.Controls.Add(this.InstalledMods);
            this.Controls.Add(this.HeaderText);
            this.Controls.Add(this.HeaderImage);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormClosingEvent);
            this.MaximizeBox = false;
            this.Name = "Filemanager_ImportMod";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "OpenCAGE - Load Mod";
            this.Load += new System.EventHandler(this.Filemanager_ImportMod_Load);
            ((System.ComponentModel.ISupportInitialize)(this.HeaderImage)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.MOD_PREVIEW)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label HeaderText;
        private System.Windows.Forms.PictureBox HeaderImage;
        private System.Windows.Forms.ListBox InstalledMods;
        private System.Windows.Forms.Button SelectMod;
        private System.Windows.Forms.Label Title1;
        private System.Windows.Forms.Button CloseButton;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label SELECTED_MOD_TITLE;
        private System.Windows.Forms.PictureBox MOD_PREVIEW;
        private System.Windows.Forms.TextBox SELECTED_MOD_DESCRIPTION;
    }
}
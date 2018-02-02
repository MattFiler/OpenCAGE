namespace Alien_Isolation_Mod_Tools
{
    partial class Landing_Main
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Landing_Main));
            this.MakeMod = new System.Windows.Forms.Button();
            this.SaveMod = new System.Windows.Forms.Button();
            this.LoadMod = new System.Windows.Forms.Button();
            this.VersionText = new System.Windows.Forms.Label();
            this.LandingBackground = new System.Windows.Forms.PictureBox();
            this.DeleteMod = new System.Windows.Forms.Button();
            this.Title1 = new System.Windows.Forms.Label();
            this.Title2 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.LandingBackground)).BeginInit();
            this.SuspendLayout();
            // 
            // MakeMod
            // 
            this.MakeMod.BackColor = System.Drawing.Color.Transparent;
            this.MakeMod.Cursor = System.Windows.Forms.Cursors.Hand;
            this.MakeMod.FlatAppearance.BorderSize = 0;
            this.MakeMod.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Transparent;
            this.MakeMod.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            this.MakeMod.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.MakeMod.Font = new System.Drawing.Font("Isolation", 48F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MakeMod.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.MakeMod.Location = new System.Drawing.Point(56, 95);
            this.MakeMod.Name = "MakeMod";
            this.MakeMod.Size = new System.Drawing.Size(242, 73);
            this.MakeMod.TabIndex = 0;
            this.MakeMod.Text = "Create";
            this.MakeMod.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.MakeMod.UseVisualStyleBackColor = false;
            this.MakeMod.Click += new System.EventHandler(this.MakeMod_Click);
            // 
            // SaveMod
            // 
            this.SaveMod.BackColor = System.Drawing.Color.Transparent;
            this.SaveMod.Cursor = System.Windows.Forms.Cursors.Hand;
            this.SaveMod.FlatAppearance.BorderSize = 0;
            this.SaveMod.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Transparent;
            this.SaveMod.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            this.SaveMod.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.SaveMod.Font = new System.Drawing.Font("Isolation", 48F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SaveMod.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.SaveMod.Location = new System.Drawing.Point(56, 158);
            this.SaveMod.Name = "SaveMod";
            this.SaveMod.Size = new System.Drawing.Size(242, 87);
            this.SaveMod.TabIndex = 1;
            this.SaveMod.Text = "Save";
            this.SaveMod.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.SaveMod.UseVisualStyleBackColor = false;
            this.SaveMod.Click += new System.EventHandler(this.LoadMod_Click);
            // 
            // LoadMod
            // 
            this.LoadMod.BackColor = System.Drawing.Color.Transparent;
            this.LoadMod.Cursor = System.Windows.Forms.Cursors.Hand;
            this.LoadMod.FlatAppearance.BorderSize = 0;
            this.LoadMod.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Transparent;
            this.LoadMod.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            this.LoadMod.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.LoadMod.Font = new System.Drawing.Font("Isolation", 48F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LoadMod.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.LoadMod.Location = new System.Drawing.Point(56, 345);
            this.LoadMod.Name = "LoadMod";
            this.LoadMod.Size = new System.Drawing.Size(242, 73);
            this.LoadMod.TabIndex = 2;
            this.LoadMod.Text = "Load";
            this.LoadMod.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.LoadMod.UseVisualStyleBackColor = false;
            this.LoadMod.Click += new System.EventHandler(this.DeleteMod_Click);
            // 
            // VersionText
            // 
            this.VersionText.AutoSize = true;
            this.VersionText.BackColor = System.Drawing.Color.Transparent;
            this.VersionText.Font = new System.Drawing.Font("Jixellation", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.VersionText.ForeColor = System.Drawing.Color.White;
            this.VersionText.Location = new System.Drawing.Point(1026, 467);
            this.VersionText.Name = "VersionText";
            this.VersionText.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.VersionText.Size = new System.Drawing.Size(288, 52);
            this.VersionText.TabIndex = 5;
            this.VersionText.Text = "Alien: Isolation Mod Tools\r\nVersion 0.0.0.0";
            this.VersionText.TextAlign = System.Drawing.ContentAlignment.BottomRight;
            // 
            // LandingBackground
            // 
            this.LandingBackground.BackgroundImage = global::Alien_Isolation_Mod_Tools.Properties.Resources.Alien_RightOfScreen;
            this.LandingBackground.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.LandingBackground.InitialImage = global::Alien_Isolation_Mod_Tools.Properties.Resources.Alien_RightOfScreen;
            this.LandingBackground.Location = new System.Drawing.Point(-2, -6);
            this.LandingBackground.Name = "LandingBackground";
            this.LandingBackground.Size = new System.Drawing.Size(1332, 553);
            this.LandingBackground.TabIndex = 3;
            this.LandingBackground.TabStop = false;
            // 
            // DeleteMod
            // 
            this.DeleteMod.BackColor = System.Drawing.Color.Transparent;
            this.DeleteMod.Cursor = System.Windows.Forms.Cursors.Hand;
            this.DeleteMod.FlatAppearance.BorderSize = 0;
            this.DeleteMod.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Transparent;
            this.DeleteMod.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            this.DeleteMod.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.DeleteMod.Font = new System.Drawing.Font("Isolation", 48F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.DeleteMod.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.DeleteMod.Location = new System.Drawing.Point(56, 408);
            this.DeleteMod.Name = "DeleteMod";
            this.DeleteMod.Size = new System.Drawing.Size(242, 88);
            this.DeleteMod.TabIndex = 7;
            this.DeleteMod.Text = "Reset";
            this.DeleteMod.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.DeleteMod.UseVisualStyleBackColor = false;
            this.DeleteMod.Click += new System.EventHandler(this.DeleteMod_Click_1);
            // 
            // Title1
            // 
            this.Title1.AutoSize = true;
            this.Title1.BackColor = System.Drawing.Color.Transparent;
            this.Title1.Font = new System.Drawing.Font("Jixellation", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Title1.ForeColor = System.Drawing.Color.White;
            this.Title1.Location = new System.Drawing.Point(41, 58);
            this.Title1.Name = "Title1";
            this.Title1.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.Title1.Size = new System.Drawing.Size(207, 35);
            this.Title1.TabIndex = 8;
            this.Title1.Text = "DEVELOP MODS";
            this.Title1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // Title2
            // 
            this.Title2.AutoSize = true;
            this.Title2.BackColor = System.Drawing.Color.Transparent;
            this.Title2.Font = new System.Drawing.Font("Jixellation", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Title2.ForeColor = System.Drawing.Color.White;
            this.Title2.Location = new System.Drawing.Point(41, 308);
            this.Title2.Name = "Title2";
            this.Title2.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.Title2.Size = new System.Drawing.Size(202, 35);
            this.Title2.TabIndex = 9;
            this.Title2.Text = "MANAGE MODS";
            this.Title2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // Landing_Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1326, 528);
            this.Controls.Add(this.Title2);
            this.Controls.Add(this.Title1);
            this.Controls.Add(this.DeleteMod);
            this.Controls.Add(this.VersionText);
            this.Controls.Add(this.LoadMod);
            this.Controls.Add(this.SaveMod);
            this.Controls.Add(this.MakeMod);
            this.Controls.Add(this.LandingBackground);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Landing_Main";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Alien: Isolation Mod Tools";
            this.Load += new System.EventHandler(this.Landing_Main_Load);
            ((System.ComponentModel.ISupportInitialize)(this.LandingBackground)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button MakeMod;
        private System.Windows.Forms.Button SaveMod;
        private System.Windows.Forms.Button LoadMod;
        private System.Windows.Forms.PictureBox LandingBackground;
        private System.Windows.Forms.Label VersionText;
        private System.Windows.Forms.Button DeleteMod;
        private System.Windows.Forms.Label Title1;
        private System.Windows.Forms.Label Title2;
    }
}
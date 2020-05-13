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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Landing_Main));
            this.OpenConfigTools = new System.Windows.Forms.Button();
            this.VersionText = new System.Windows.Forms.Label();
            this.LandingBackground = new System.Windows.Forms.PictureBox();
            this.LaunchGame = new System.Windows.Forms.Button();
            this.OpenContentTools = new System.Windows.Forms.Button();
            this.OpenExperimentalTools = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.LandingBackground)).BeginInit();
            this.SuspendLayout();
            // 
            // OpenConfigTools
            // 
            this.OpenConfigTools.BackColor = System.Drawing.Color.Transparent;
            this.OpenConfigTools.Cursor = System.Windows.Forms.Cursors.Hand;
            this.OpenConfigTools.FlatAppearance.BorderSize = 0;
            this.OpenConfigTools.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Transparent;
            this.OpenConfigTools.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            this.OpenConfigTools.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.OpenConfigTools.Font = new System.Drawing.Font("Isolation", 48F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.OpenConfigTools.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.OpenConfigTools.Location = new System.Drawing.Point(32, 104);
            this.OpenConfigTools.Name = "OpenConfigTools";
            this.OpenConfigTools.Size = new System.Drawing.Size(432, 73);
            this.OpenConfigTools.TabIndex = 0;
            this.OpenConfigTools.Text = "Config Tools";
            this.OpenConfigTools.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.toolTip1.SetToolTip(this.OpenConfigTools, "Tools for editing a range of game configurations, and saving/exporting any change" +
        "s.");
            this.OpenConfigTools.UseVisualStyleBackColor = false;
            this.OpenConfigTools.Click += new System.EventHandler(this.MakeMod_Click);
            // 
            // VersionText
            // 
            this.VersionText.AutoSize = true;
            this.VersionText.BackColor = System.Drawing.Color.Transparent;
            this.VersionText.Font = new System.Drawing.Font("Jixellation", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.VersionText.ForeColor = System.Drawing.Color.White;
            this.VersionText.Location = new System.Drawing.Point(1147, 493);
            this.VersionText.Name = "VersionText";
            this.VersionText.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.VersionText.Size = new System.Drawing.Size(167, 26);
            this.VersionText.TabIndex = 5;
            this.VersionText.Text = "Version 0.0.0.0";
            this.VersionText.TextAlign = System.Drawing.ContentAlignment.BottomRight;
            // 
            // LandingBackground
            // 
            this.LandingBackground.BackgroundImage = global::Alien_Isolation_Mod_Tools.Properties.Resources.landing;
            this.LandingBackground.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.LandingBackground.InitialImage = global::Alien_Isolation_Mod_Tools.Properties.Resources.Alien_RightOfScreen;
            this.LandingBackground.Location = new System.Drawing.Point(-2, -11);
            this.LandingBackground.Name = "LandingBackground";
            this.LandingBackground.Size = new System.Drawing.Size(1332, 553);
            this.LandingBackground.TabIndex = 3;
            this.LandingBackground.TabStop = false;
            // 
            // LaunchGame
            // 
            this.LaunchGame.BackColor = System.Drawing.Color.Transparent;
            this.LaunchGame.Cursor = System.Windows.Forms.Cursors.Hand;
            this.LaunchGame.FlatAppearance.BorderSize = 0;
            this.LaunchGame.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Transparent;
            this.LaunchGame.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            this.LaunchGame.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.LaunchGame.Font = new System.Drawing.Font("Isolation", 48F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LaunchGame.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.LaunchGame.Location = new System.Drawing.Point(32, 352);
            this.LaunchGame.Name = "LaunchGame";
            this.LaunchGame.Size = new System.Drawing.Size(443, 73);
            this.LaunchGame.TabIndex = 10;
            this.LaunchGame.Text = "Launch Game";
            this.LaunchGame.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.toolTip1.SetToolTip(this.LaunchGame, "Launch the game to a specific map.");
            this.LaunchGame.UseVisualStyleBackColor = false;
            this.LaunchGame.Click += new System.EventHandler(this.LaunchGame_Click);
            // 
            // OpenContentTools
            // 
            this.OpenContentTools.BackColor = System.Drawing.Color.Transparent;
            this.OpenContentTools.Cursor = System.Windows.Forms.Cursors.Hand;
            this.OpenContentTools.FlatAppearance.BorderSize = 0;
            this.OpenContentTools.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Transparent;
            this.OpenContentTools.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            this.OpenContentTools.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.OpenContentTools.Font = new System.Drawing.Font("Isolation", 48F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.OpenContentTools.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.OpenContentTools.Location = new System.Drawing.Point(32, 177);
            this.OpenContentTools.Name = "OpenContentTools";
            this.OpenContentTools.Size = new System.Drawing.Size(468, 73);
            this.OpenContentTools.TabIndex = 12;
            this.OpenContentTools.Text = "Content Tools";
            this.OpenContentTools.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.toolTip1.SetToolTip(this.OpenContentTools, "Tools for editing game content (E.G. textures, UI).");
            this.OpenContentTools.UseVisualStyleBackColor = false;
            this.OpenContentTools.Click += new System.EventHandler(this.OpenContentTools_Click);
            // 
            // OpenExperimentalTools
            // 
            this.OpenExperimentalTools.BackColor = System.Drawing.Color.Transparent;
            this.OpenExperimentalTools.Cursor = System.Windows.Forms.Cursors.Hand;
            this.OpenExperimentalTools.FlatAppearance.BorderSize = 0;
            this.OpenExperimentalTools.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Transparent;
            this.OpenExperimentalTools.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            this.OpenExperimentalTools.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.OpenExperimentalTools.Font = new System.Drawing.Font("Isolation", 48F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.OpenExperimentalTools.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.OpenExperimentalTools.Location = new System.Drawing.Point(32, 250);
            this.OpenExperimentalTools.Name = "OpenExperimentalTools";
            this.OpenExperimentalTools.Size = new System.Drawing.Size(606, 73);
            this.OpenExperimentalTools.TabIndex = 14;
            this.OpenExperimentalTools.Text = "Experimental Tools";
            this.OpenExperimentalTools.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.toolTip1.SetToolTip(this.OpenExperimentalTools, "Experimental tools that provide a range of other features.");
            this.OpenExperimentalTools.UseVisualStyleBackColor = false;
            this.OpenExperimentalTools.Click += new System.EventHandler(this.OpenExperimentalTools_Click);
            // 
            // Landing_Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1326, 528);
            this.Controls.Add(this.OpenExperimentalTools);
            this.Controls.Add(this.OpenContentTools);
            this.Controls.Add(this.LaunchGame);
            this.Controls.Add(this.VersionText);
            this.Controls.Add(this.OpenConfigTools);
            this.Controls.Add(this.LandingBackground);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Landing_Main";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "OpenCAGE";
            this.Load += new System.EventHandler(this.Landing_Main_Load);
            ((System.ComponentModel.ISupportInitialize)(this.LandingBackground)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button OpenConfigTools;
        private System.Windows.Forms.PictureBox LandingBackground;
        private System.Windows.Forms.Label VersionText;
        private System.Windows.Forms.Button LaunchGame;
        private System.Windows.Forms.Button OpenContentTools;
        private System.Windows.Forms.Button OpenExperimentalTools;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
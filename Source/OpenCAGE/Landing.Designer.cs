namespace OpenCAGE
{
    partial class Landing
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Landing));
            this.OpenConfigTools = new System.Windows.Forms.Button();
            this.VersionText = new System.Windows.Forms.Label();
            this.LaunchGame = new System.Windows.Forms.Button();
            this.OpenContentTools = new System.Windows.Forms.Button();
            this.OpenScriptingTools = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.OpenBehaviourTreeTools = new System.Windows.Forms.Button();
            this.DebugText = new System.Windows.Forms.Label();
            this.LandingBackground = new System.Windows.Forms.PictureBox();
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
            this.OpenConfigTools.Font = new System.Drawing.Font("Isolation", 45F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.OpenConfigTools.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.OpenConfigTools.Location = new System.Drawing.Point(32, 148);
            this.OpenConfigTools.Name = "OpenConfigTools";
            this.OpenConfigTools.Size = new System.Drawing.Size(680, 73);
            this.OpenConfigTools.TabIndex = 0;
            this.OpenConfigTools.Text = "Edit Configurations";
            this.OpenConfigTools.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.toolTip1.SetToolTip(this.OpenConfigTools, "Tools for editing a range of game configurations, and saving/exporting any change" +
        "s.");
            this.OpenConfigTools.UseVisualStyleBackColor = false;
            this.OpenConfigTools.Click += new System.EventHandler(this.OpenConfigTools_Click);
            // 
            // VersionText
            // 
            this.VersionText.AutoSize = true;
            this.VersionText.BackColor = System.Drawing.Color.Transparent;
            this.VersionText.Font = new System.Drawing.Font("Jixellation", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.VersionText.ForeColor = System.Drawing.Color.White;
            this.VersionText.Location = new System.Drawing.Point(1147, 493);
            this.VersionText.Name = "VersionText";
            this.VersionText.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.VersionText.Size = new System.Drawing.Size(167, 26);
            this.VersionText.TabIndex = 5;
            this.VersionText.Text = "Version 0.0.0.0";
            this.VersionText.TextAlign = System.Drawing.ContentAlignment.BottomRight;
            // 
            // LaunchGame
            // 
            this.LaunchGame.BackColor = System.Drawing.Color.Transparent;
            this.LaunchGame.Cursor = System.Windows.Forms.Cursors.Hand;
            this.LaunchGame.FlatAppearance.BorderSize = 0;
            this.LaunchGame.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Transparent;
            this.LaunchGame.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            this.LaunchGame.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.LaunchGame.Font = new System.Drawing.Font("Isolation", 45F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LaunchGame.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.LaunchGame.Location = new System.Drawing.Point(32, 394);
            this.LaunchGame.Name = "LaunchGame";
            this.LaunchGame.Size = new System.Drawing.Size(443, 73);
            this.LaunchGame.TabIndex = 10;
            this.LaunchGame.Text = "Launch Game";
            this.LaunchGame.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.toolTip1.SetToolTip(this.LaunchGame, "Launch the game with various options available.\r\n");
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
            this.OpenContentTools.Font = new System.Drawing.Font("Isolation", 45F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.OpenContentTools.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.OpenContentTools.Location = new System.Drawing.Point(32, 75);
            this.OpenContentTools.Name = "OpenContentTools";
            this.OpenContentTools.Size = new System.Drawing.Size(680, 73);
            this.OpenContentTools.TabIndex = 12;
            this.OpenContentTools.Text = "Edit Assets";
            this.OpenContentTools.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.toolTip1.SetToolTip(this.OpenContentTools, "Tools for editing various game assets (E.G. textures, UI).");
            this.OpenContentTools.UseVisualStyleBackColor = false;
            this.OpenContentTools.Click += new System.EventHandler(this.OpenContentTools_Click);
            // 
            // OpenScriptingTools
            // 
            this.OpenScriptingTools.BackColor = System.Drawing.Color.Transparent;
            this.OpenScriptingTools.Cursor = System.Windows.Forms.Cursors.Hand;
            this.OpenScriptingTools.FlatAppearance.BorderSize = 0;
            this.OpenScriptingTools.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Transparent;
            this.OpenScriptingTools.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            this.OpenScriptingTools.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.OpenScriptingTools.Font = new System.Drawing.Font("Isolation", 45F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.OpenScriptingTools.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.OpenScriptingTools.Location = new System.Drawing.Point(32, 221);
            this.OpenScriptingTools.Name = "OpenScriptingTools";
            this.OpenScriptingTools.Size = new System.Drawing.Size(680, 73);
            this.OpenScriptingTools.TabIndex = 14;
            this.OpenScriptingTools.Text = "Edit Cathode Scripts";
            this.OpenScriptingTools.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.toolTip1.SetToolTip(this.OpenScriptingTools, "Tools for editing the game\'s Cathode scripting format.");
            this.OpenScriptingTools.UseVisualStyleBackColor = false;
            this.OpenScriptingTools.Click += new System.EventHandler(this.OpenScriptingTools_Click);
            // 
            // OpenBehaviourTreeTools
            // 
            this.OpenBehaviourTreeTools.BackColor = System.Drawing.Color.Transparent;
            this.OpenBehaviourTreeTools.Cursor = System.Windows.Forms.Cursors.Hand;
            this.OpenBehaviourTreeTools.FlatAppearance.BorderSize = 0;
            this.OpenBehaviourTreeTools.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Transparent;
            this.OpenBehaviourTreeTools.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            this.OpenBehaviourTreeTools.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.OpenBehaviourTreeTools.Font = new System.Drawing.Font("Isolation", 45F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.OpenBehaviourTreeTools.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.OpenBehaviourTreeTools.Location = new System.Drawing.Point(32, 294);
            this.OpenBehaviourTreeTools.Name = "OpenBehaviourTreeTools";
            this.OpenBehaviourTreeTools.Size = new System.Drawing.Size(680, 73);
            this.OpenBehaviourTreeTools.TabIndex = 16;
            this.OpenBehaviourTreeTools.Text = "Edit Behaviour Trees";
            this.OpenBehaviourTreeTools.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.toolTip1.SetToolTip(this.OpenBehaviourTreeTools, "Tools for editing the game\'s behaviour trees.\r\n");
            this.OpenBehaviourTreeTools.UseVisualStyleBackColor = false;
            this.OpenBehaviourTreeTools.Click += new System.EventHandler(this.OpenBehaviourTreeTools_Click);
            // 
            // DebugText
            // 
            this.DebugText.AutoSize = true;
            this.DebugText.BackColor = System.Drawing.Color.Transparent;
            this.DebugText.Font = new System.Drawing.Font("Jixellation", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.DebugText.ForeColor = System.Drawing.Color.White;
            this.DebugText.Location = new System.Drawing.Point(32, 493);
            this.DebugText.Name = "DebugText";
            this.DebugText.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.DebugText.Size = new System.Drawing.Size(0, 26);
            this.DebugText.TabIndex = 15;
            this.DebugText.TextAlign = System.Drawing.ContentAlignment.BottomRight;
            // 
            // LandingBackground
            // 
            this.LandingBackground.BackgroundImage = global::OpenCAGE.Properties.Resources.landing_min;
            this.LandingBackground.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.LandingBackground.Location = new System.Drawing.Point(-2, -11);
            this.LandingBackground.Name = "LandingBackground";
            this.LandingBackground.Size = new System.Drawing.Size(1332, 553);
            this.LandingBackground.TabIndex = 3;
            this.LandingBackground.TabStop = false;
            // 
            // Landing
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1326, 528);
            this.Controls.Add(this.OpenBehaviourTreeTools);
            this.Controls.Add(this.DebugText);
            this.Controls.Add(this.OpenScriptingTools);
            this.Controls.Add(this.OpenContentTools);
            this.Controls.Add(this.LaunchGame);
            this.Controls.Add(this.VersionText);
            this.Controls.Add(this.OpenConfigTools);
            this.Controls.Add(this.LandingBackground);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "Landing";
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
        private System.Windows.Forms.Button OpenScriptingTools;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Label DebugText;
        private System.Windows.Forms.Button OpenBehaviourTreeTools;
    }
}
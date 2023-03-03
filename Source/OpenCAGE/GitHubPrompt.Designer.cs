namespace OpenCAGE
{
    partial class GitHubPrompt
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GitHubPrompt));
            this.OpenGitHub = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.LandingBackground = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.LandingBackground)).BeginInit();
            this.SuspendLayout();
            // 
            // OpenGitHub
            // 
            this.OpenGitHub.BackColor = System.Drawing.Color.Transparent;
            this.OpenGitHub.Cursor = System.Windows.Forms.Cursors.Hand;
            this.OpenGitHub.FlatAppearance.BorderSize = 0;
            this.OpenGitHub.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Transparent;
            this.OpenGitHub.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            this.OpenGitHub.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.OpenGitHub.Font = new System.Drawing.Font("Isolation", 45F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.OpenGitHub.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.OpenGitHub.Location = new System.Drawing.Point(-11, -11);
            this.OpenGitHub.Name = "OpenGitHub";
            this.OpenGitHub.Size = new System.Drawing.Size(897, 369);
            this.OpenGitHub.TabIndex = 12;
            this.OpenGitHub.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.OpenGitHub.UseVisualStyleBackColor = false;
            this.OpenGitHub.Click += new System.EventHandler(this.OpenGitHub_Click);
            // 
            // LandingBackground
            // 
            this.LandingBackground.BackgroundImage = global::OpenCAGE.Properties.Resources.github_prompt;
            this.LandingBackground.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.LandingBackground.Location = new System.Drawing.Point(-2, -11);
            this.LandingBackground.Name = "LandingBackground";
            this.LandingBackground.Size = new System.Drawing.Size(888, 369);
            this.LandingBackground.TabIndex = 3;
            this.LandingBackground.TabStop = false;
            // 
            // GitHubPrompt
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(879, 339);
            this.Controls.Add(this.OpenGitHub);
            this.Controls.Add(this.LandingBackground);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "GitHubPrompt";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Before you continue...";
            ((System.ComponentModel.ISupportInitialize)(this.LandingBackground)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.PictureBox LandingBackground;
        private System.Windows.Forms.Button OpenGitHub;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
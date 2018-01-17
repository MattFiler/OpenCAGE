namespace PackagingTool
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Landing));
            this.openBehaviourTreePackager = new System.Windows.Forms.Button();
            this.openCharEd = new System.Windows.Forms.Button();
            this.openAlienConfig = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // openBehaviourTreePackager
            // 
            this.openBehaviourTreePackager.Location = new System.Drawing.Point(12, 12);
            this.openBehaviourTreePackager.Name = "openBehaviourTreePackager";
            this.openBehaviourTreePackager.Size = new System.Drawing.Size(229, 35);
            this.openBehaviourTreePackager.TabIndex = 0;
            this.openBehaviourTreePackager.Text = "Behaviour Tree Packager";
            this.openBehaviourTreePackager.UseVisualStyleBackColor = true;
            this.openBehaviourTreePackager.Click += new System.EventHandler(this.openBehaviourTreePackager_Click);
            // 
            // openCharEd
            // 
            this.openCharEd.Location = new System.Drawing.Point(12, 53);
            this.openCharEd.Name = "openCharEd";
            this.openCharEd.Size = new System.Drawing.Size(229, 35);
            this.openCharEd.TabIndex = 1;
            this.openCharEd.Text = "Character Attribute Editor";
            this.openCharEd.UseVisualStyleBackColor = true;
            this.openCharEd.Click += new System.EventHandler(this.button2_Click);
            // 
            // openAlienConfig
            // 
            this.openAlienConfig.Location = new System.Drawing.Point(12, 94);
            this.openAlienConfig.Name = "openAlienConfig";
            this.openAlienConfig.Size = new System.Drawing.Size(229, 35);
            this.openAlienConfig.TabIndex = 2;
            this.openAlienConfig.Text = "Alien Configuration Editor";
            this.openAlienConfig.UseVisualStyleBackColor = true;
            this.openAlienConfig.Click += new System.EventHandler(this.openAlienConfig_Click);
            // 
            // Landing
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(251, 138);
            this.Controls.Add(this.openAlienConfig);
            this.Controls.Add(this.openCharEd);
            this.Controls.Add(this.openBehaviourTreePackager);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Landing";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Alien: Isolation Behaviour Tool";
            this.Load += new System.EventHandler(this.Landing_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button openBehaviourTreePackager;
        private System.Windows.Forms.Button openCharEd;
        private System.Windows.Forms.Button openAlienConfig;
    }
}
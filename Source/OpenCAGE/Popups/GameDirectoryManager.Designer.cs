namespace OpenCAGE.Popups
{
    partial class GameDirectoryManager
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GameDirectoryManager));
            this.registerNew = new System.Windows.Forms.Button();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.launchWithoutViewport = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // registerNew
            // 
            this.registerNew.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.registerNew.Location = new System.Drawing.Point(12, 334);
            this.registerNew.Name = "registerNew";
            this.registerNew.Size = new System.Drawing.Size(244, 23);
            this.registerNew.TabIndex = 2;
            this.registerNew.Text = "Register New Game Install";
            this.registerNew.UseVisualStyleBackColor = true;
            this.registerNew.Click += new System.EventHandler(this.registerNew_Click);
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.flowLayoutPanel1.AutoScroll = true;
            this.flowLayoutPanel1.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(12, 7);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(873, 321);
            this.flowLayoutPanel1.TabIndex = 1;
            this.flowLayoutPanel1.WrapContents = false;
            // 
            // launchWithoutViewport
            // 
            this.launchWithoutViewport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.launchWithoutViewport.AutoSize = true;
            this.launchWithoutViewport.Location = new System.Drawing.Point(574, 340);
            this.launchWithoutViewport.Name = "launchWithoutViewport";
            this.launchWithoutViewport.Size = new System.Drawing.Size(311, 17);
            this.launchWithoutViewport.TabIndex = 3;
            this.launchWithoutViewport.Text = "Launch other editors without viewport (saves memory usage)";
            this.launchWithoutViewport.UseVisualStyleBackColor = true;
            this.launchWithoutViewport.CheckedChanged += new System.EventHandler(this.launchWithoutViewport_CheckedChanged);
            // 
            // GameDirectoryManager
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(892, 368);
            this.Controls.Add(this.launchWithoutViewport);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Controls.Add(this.registerNew);
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(908, 1500);
            this.MinimumSize = new System.Drawing.Size(908, 100);
            this.Name = "GameDirectoryManager";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Game Directory Manager";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button registerNew;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.CheckBox launchWithoutViewport;
    }
}
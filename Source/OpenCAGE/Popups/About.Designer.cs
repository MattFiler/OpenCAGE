namespace OpenCAGE.Popups
{
    partial class About
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(About));
            this.aboutHost = new System.Windows.Forms.Integration.ElementHost();
            this.SuspendLayout();
            // 
            // aboutHost
            // 
            this.aboutHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.aboutHost.Location = new System.Drawing.Point(0, 0);
            this.aboutHost.Name = "aboutHost";
            this.aboutHost.Size = new System.Drawing.Size(1100, 489);
            this.aboutHost.TabIndex = 14;
            this.aboutHost.Text = "elementHost1";
            this.aboutHost.Child = null;
            // 
            // About
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1100, 489);
            this.Controls.Add(this.aboutHost);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.Name = "About";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "About";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Integration.ElementHost aboutHost;
    }
}

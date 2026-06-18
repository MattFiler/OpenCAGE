namespace OpenCAGE.DockPanels
{
    partial class LevelViewerPanel
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.embeddedWindowHost = new OpenCAGE.EmbeddedWindowHost();
            this.loadingLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // embeddedWindowHost
            // 
            this.embeddedWindowHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.embeddedWindowHost.Location = new System.Drawing.Point(0, 0);
            this.embeddedWindowHost.Name = "embeddedWindowHost";
            this.embeddedWindowHost.Size = new System.Drawing.Size(800, 300);
            this.embeddedWindowHost.TabIndex = 0;
            // 
            // loadingLabel
            // 
            this.loadingLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.loadingLabel.Location = new System.Drawing.Point(0, 0);
            this.loadingLabel.Name = "loadingLabel";
            this.loadingLabel.Size = new System.Drawing.Size(800, 300);
            this.loadingLabel.TabIndex = 1;
            this.loadingLabel.Text = "Initialising viewport...";
            this.loadingLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.loadingLabel.Visible = false;
            // 
            // LevelViewerPanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 300);
            this.Controls.Add(this.loadingLabel);
            this.Controls.Add(this.embeddedWindowHost);
            this.DockAreas = ((WeifenLuo.WinFormsUI.Docking.DockAreas)((WeifenLuo.WinFormsUI.Docking.DockAreas.Float | WeifenLuo.WinFormsUI.Docking.DockAreas.DockTop)));
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.Name = "LevelViewerPanel";
            this.ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.DockTop;
            this.Text = "Viewport";
            this.ResumeLayout(false);
        }

        private OpenCAGE.EmbeddedWindowHost embeddedWindowHost;
        private System.Windows.Forms.Label loadingLabel;
    }
}

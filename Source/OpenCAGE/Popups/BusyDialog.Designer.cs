#if ENABLE_MOD_PACKAGES
namespace OpenCAGE.Popups
{
    partial class BusyDialog
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
            this._bar = new System.Windows.Forms.ProgressBar();
            this._label = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // _bar
            // 
            this._bar.Dock = System.Windows.Forms.DockStyle.Top;
            this._bar.Location = new System.Drawing.Point(12, 56);
            this._bar.MarqueeAnimationSpeed = 30;
            this._bar.Name = "_bar";
            this._bar.Size = new System.Drawing.Size(430, 20);
            this._bar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this._bar.TabIndex = 0;
            // 
            // _label
            // 
            this._label.AutoEllipsis = true;
            this._label.Dock = System.Windows.Forms.DockStyle.Top;
            this._label.Location = new System.Drawing.Point(12, 12);
            this._label.Name = "_label";
            this._label.Padding = new System.Windows.Forms.Padding(8);
            this._label.Size = new System.Drawing.Size(430, 44);
            this._label.TabIndex = 1;
            this._label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // BusyDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(454, 101);
            this.Controls.Add(this._bar);
            this.Controls.Add(this._label);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "BusyDialog";
            this.Padding = new System.Windows.Forms.Padding(12);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "OpenCAGE";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.BusyDialog_FormClosing);
            this.Shown += new System.EventHandler(this.BusyDialog_Shown);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ProgressBar _bar;
        private System.Windows.Forms.Label _label;
    }
}
#endif

#if ENABLE_MOD_PACKAGES
namespace OpenCAGE.Popups
{
    partial class ModReportDialog
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
            this._body = new System.Windows.Forms.TextBox();
            this._buttons = new System.Windows.Forms.FlowLayoutPanel();
            this._cancelButton = new System.Windows.Forms.Button();
            this._otherButton = new System.Windows.Forms.Button();
            this._okButton = new System.Windows.Forms.Button();
            this._buttons.SuspendLayout();
            this.SuspendLayout();
            // 
            // _body
            // 
            this._body.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this._body.Dock = System.Windows.Forms.DockStyle.Fill;
            this._body.Location = new System.Drawing.Point(12, 12);
            this._body.Multiline = true;
            this._body.Name = "_body";
            this._body.ReadOnly = true;
            this._body.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._body.Size = new System.Drawing.Size(680, 365);
            this._body.TabIndex = 0;
            // 
            // _buttons
            // 
            this._buttons.Controls.Add(this._cancelButton);
            this._buttons.Controls.Add(this._otherButton);
            this._buttons.Controls.Add(this._okButton);
            this._buttons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this._buttons.Location = new System.Drawing.Point(12, 377);
            this._buttons.Name = "_buttons";
            this._buttons.Padding = new System.Windows.Forms.Padding(6);
            this._buttons.Size = new System.Drawing.Size(680, 44);
            this._buttons.TabIndex = 1;
            // 
            // _cancelButton
            // 
            this._cancelButton.AutoSize = true;
            this._cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._cancelButton.Location = new System.Drawing.Point(575, 9);
            this._cancelButton.MinimumSize = new System.Drawing.Size(90, 30);
            this._cancelButton.Name = "_cancelButton";
            this._cancelButton.Size = new System.Drawing.Size(90, 30);
            this._cancelButton.TabIndex = 0;
            this._cancelButton.Text = "Cancel";
            // 
            // _otherButton
            // 
            this._otherButton.AutoSize = true;
            this._otherButton.DialogResult = System.Windows.Forms.DialogResult.No;
            this._otherButton.Location = new System.Drawing.Point(479, 9);
            this._otherButton.MinimumSize = new System.Drawing.Size(90, 30);
            this._otherButton.Name = "_otherButton";
            this._otherButton.Size = new System.Drawing.Size(90, 30);
            this._otherButton.TabIndex = 1;
            this._otherButton.Text = "No";
            // 
            // _okButton
            // 
            this._okButton.AutoSize = true;
            this._okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this._okButton.Location = new System.Drawing.Point(383, 9);
            this._okButton.MinimumSize = new System.Drawing.Size(90, 30);
            this._okButton.Name = "_okButton";
            this._okButton.Size = new System.Drawing.Size(90, 30);
            this._okButton.TabIndex = 2;
            this._okButton.Text = "OK";
            // 
            // ModReportDialog
            // 
            this.AcceptButton = this._okButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._cancelButton;
            this.ClientSize = new System.Drawing.Size(704, 421);
            this.Controls.Add(this._body);
            this.Controls.Add(this._buttons);
            this.MinimumSize = new System.Drawing.Size(480, 300);
            this.Name = "ModReportDialog";
            this.Padding = new System.Windows.Forms.Padding(12, 12, 12, 0);
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Shown += new System.EventHandler(this.ModReportDialog_Shown);
            this._buttons.ResumeLayout(false);
            this._buttons.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox _body;
        private System.Windows.Forms.FlowLayoutPanel _buttons;
        private System.Windows.Forms.Button _cancelButton;
        private System.Windows.Forms.Button _otherButton;
        private System.Windows.Forms.Button _okButton;
    }
}
#endif

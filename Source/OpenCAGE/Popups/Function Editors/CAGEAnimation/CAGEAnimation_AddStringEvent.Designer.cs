namespace OpenCAGE
{
    partial class CAGEAnimation_AddStringEvent
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
            this._prompt = new System.Windows.Forms.Label();
            this._eventName = new System.Windows.Forms.TextBox();
            this._ok = new System.Windows.Forms.Button();
            this._cancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // _prompt
            //
            this._prompt.AutoSize = true;
            this._prompt.Location = new System.Drawing.Point(12, 14);
            this._prompt.Name = "_prompt";
            this._prompt.Size = new System.Drawing.Size(93, 13);
            this._prompt.TabIndex = 0;
            this._prompt.Text = "Event name at 0s:";
            //
            // _eventName
            //
            this._eventName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this._eventName.Location = new System.Drawing.Point(12, 38);
            this._eventName.Name = "_eventName";
            this._eventName.Size = new System.Drawing.Size(392, 20);
            this._eventName.TabIndex = 1;
            //
            // _ok
            //
            this._ok.Location = new System.Drawing.Point(248, 78);
            this._ok.Name = "_ok";
            this._ok.Size = new System.Drawing.Size(75, 26);
            this._ok.TabIndex = 2;
            this._ok.Text = "Add";
            this._ok.Click += new System.EventHandler(this.Ok_Click);
            //
            // _cancel
            //
            this._cancel.Location = new System.Drawing.Point(329, 78);
            this._cancel.Name = "_cancel";
            this._cancel.Size = new System.Drawing.Size(75, 26);
            this._cancel.TabIndex = 3;
            this._cancel.Text = "Cancel";
            this._cancel.Click += new System.EventHandler(this.Cancel_Click);
            //
            // CAGEAnimation_AddStringEvent
            //
            this.AcceptButton = this._ok;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._cancel;
            this.ClientSize = new System.Drawing.Size(420, 120);
            this.Controls.Add(this._prompt);
            this.Controls.Add(this._eventName);
            this.Controls.Add(this._ok);
            this.Controls.Add(this._cancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CAGEAnimation_AddStringEvent";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Add String Event";
            this.Shown += new System.EventHandler(this.CAGEAnimation_AddStringEvent_Shown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label _prompt;
        private System.Windows.Forms.TextBox _eventName;
        private System.Windows.Forms.Button _ok;
        private System.Windows.Forms.Button _cancel;
    }
}
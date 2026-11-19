#if ENABLE_MOD_PACKAGES
namespace OpenCAGE.Popups
{
    partial class PristineCaptureWizard
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
            this._headline = new System.Windows.Forms.Label();
            this._body = new System.Windows.Forms.TextBox();
            this._fileList = new System.Windows.Forms.ListView();
            this._fileColumn = new System.Windows.Forms.ColumnHeader();
            this._stateColumn = new System.Windows.Forms.ColumnHeader();
            this._buttons = new System.Windows.Forms.FlowLayoutPanel();
            this._closeButton = new System.Windows.Forms.Button();
            this._secondaryButton = new System.Windows.Forms.Button();
            this._actionButton = new System.Windows.Forms.Button();
            this._buttons.SuspendLayout();
            this.SuspendLayout();
            // 
            // _headline
            // 
            this._headline.Dock = System.Windows.Forms.DockStyle.Top;
            this._headline.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
            this._headline.Location = new System.Drawing.Point(10, 10);
            this._headline.Name = "_headline";
            this._headline.Padding = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this._headline.Size = new System.Drawing.Size(724, 30);
            this._headline.TabIndex = 2;
            this._headline.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _body
            // 
            this._body.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this._body.Dock = System.Windows.Forms.DockStyle.Top;
            this._body.Location = new System.Drawing.Point(10, 40);
            this._body.Multiline = true;
            this._body.Name = "_body";
            this._body.ReadOnly = true;
            this._body.Size = new System.Drawing.Size(724, 150);
            this._body.TabIndex = 1;
            // 
            // _fileList
            // 
            this._fileList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._fileColumn,
            this._stateColumn});
            this._fileList.Dock = System.Windows.Forms.DockStyle.Fill;
            this._fileList.FullRowSelect = true;
            this._fileList.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this._fileList.Location = new System.Drawing.Point(10, 190);
            this._fileList.Name = "_fileList";
            this._fileList.Size = new System.Drawing.Size(724, 279);
            this._fileList.TabIndex = 0;
            this._fileList.View = System.Windows.Forms.View.Details;
            // 
            // _fileColumn
            // 
            this._fileColumn.Text = "File";
            this._fileColumn.Width = 480;
            // 
            // _stateColumn
            // 
            this._stateColumn.Text = "";
            this._stateColumn.Width = 140;
            // 
            // _buttons
            // 
            this._buttons.Controls.Add(this._closeButton);
            this._buttons.Controls.Add(this._secondaryButton);
            this._buttons.Controls.Add(this._actionButton);
            this._buttons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this._buttons.Location = new System.Drawing.Point(10, 469);
            this._buttons.Name = "_buttons";
            this._buttons.Padding = new System.Windows.Forms.Padding(6);
            this._buttons.Size = new System.Drawing.Size(724, 42);
            this._buttons.TabIndex = 3;
            // 
            // _closeButton
            // 
            this._closeButton.AutoSize = true;
            this._closeButton.Location = new System.Drawing.Point(634, 9);
            this._closeButton.Name = "_closeButton";
            this._closeButton.Size = new System.Drawing.Size(75, 28);
            this._closeButton.TabIndex = 0;
            this._closeButton.Text = "Cancel";
            this._closeButton.Click += new System.EventHandler(this._closeButton_Click);
            // 
            // _secondaryButton
            // 
            this._secondaryButton.AutoSize = true;
            this._secondaryButton.Location = new System.Drawing.Point(553, 9);
            this._secondaryButton.Name = "_secondaryButton";
            this._secondaryButton.Size = new System.Drawing.Size(75, 28);
            this._secondaryButton.TabIndex = 1;
            this._secondaryButton.Visible = false;
            this._secondaryButton.Click += new System.EventHandler(this._secondaryButton_Click);
            // 
            // _actionButton
            // 
            this._actionButton.AutoSize = true;
            this._actionButton.Location = new System.Drawing.Point(472, 9);
            this._actionButton.Name = "_actionButton";
            this._actionButton.Size = new System.Drawing.Size(75, 28);
            this._actionButton.TabIndex = 2;
            this._actionButton.Click += new System.EventHandler(this._actionButton_Click);
            // 
            // PristineCaptureWizard
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(744, 521);
            this.Controls.Add(this._fileList);
            this.Controls.Add(this._body);
            this.Controls.Add(this._headline);
            this.Controls.Add(this._buttons);
            this.MinimumSize = new System.Drawing.Size(620, 420);
            this.Name = "PristineCaptureWizard";
            this.Padding = new System.Windows.Forms.Padding(10);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Keep Your Changes Safe";
            this._buttons.ResumeLayout(false);
            this._buttons.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label _headline;
        private System.Windows.Forms.TextBox _body;
        private System.Windows.Forms.ListView _fileList;
        private System.Windows.Forms.ColumnHeader _fileColumn;
        private System.Windows.Forms.ColumnHeader _stateColumn;
        private System.Windows.Forms.FlowLayoutPanel _buttons;
        private System.Windows.Forms.Button _closeButton;
        private System.Windows.Forms.Button _secondaryButton;
        private System.Windows.Forms.Button _actionButton;
    }
}
#endif

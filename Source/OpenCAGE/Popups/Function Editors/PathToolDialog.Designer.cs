namespace OpenCAGE
{
    partial class PathToolDialog
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
            this.reportPanel = new System.Windows.Forms.Panel();
            this._report = new System.Windows.Forms.TextBox();
            this.buttonsPanel = new System.Windows.Forms.FlowLayoutPanel();
            this._close = new System.Windows.Forms.Button();
            this._apply = new System.Windows.Forms.Button();
            this._check = new System.Windows.Forms.Button();
            this._status = new System.Windows.Forms.Label();
            this._rows = new System.Windows.Forms.TableLayoutPanel();
            this.introLabel = new System.Windows.Forms.Label();
            this.reportPanel.SuspendLayout();
            this.buttonsPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // reportPanel
            //
            this.reportPanel.Controls.Add(this._report);
            this.reportPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.reportPanel.Location = new System.Drawing.Point(0, 64);
            this.reportPanel.Name = "reportPanel";
            this.reportPanel.Padding = new System.Windows.Forms.Padding(8);
            this.reportPanel.Size = new System.Drawing.Size(544, 499);
            this.reportPanel.TabIndex = 0;
            //
            // _report
            //
            this._report.Dock = System.Windows.Forms.DockStyle.Fill;
            this._report.Font = new System.Drawing.Font("Courier New", 8.25F);
            this._report.Location = new System.Drawing.Point(8, 8);
            this._report.Multiline = true;
            this._report.Name = "_report";
            this._report.ReadOnly = true;
            this._report.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this._report.Size = new System.Drawing.Size(528, 483);
            this._report.TabIndex = 0;
            this._report.WordWrap = false;
            //
            // buttonsPanel
            //
            this.buttonsPanel.Controls.Add(this._close);
            this.buttonsPanel.Controls.Add(this._apply);
            this.buttonsPanel.Controls.Add(this._check);
            this.buttonsPanel.Controls.Add(this._status);
            this.buttonsPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.buttonsPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.buttonsPanel.Location = new System.Drawing.Point(0, 563);
            this.buttonsPanel.Name = "buttonsPanel";
            this.buttonsPanel.Padding = new System.Windows.Forms.Padding(6);
            this.buttonsPanel.Size = new System.Drawing.Size(544, 38);
            this.buttonsPanel.TabIndex = 1;
            //
            // _close
            //
            this._close.Location = new System.Drawing.Point(439, 9);
            this._close.Name = "_close";
            this._close.Size = new System.Drawing.Size(90, 23);
            this._close.TabIndex = 0;
            this._close.Text = "Close";
            this._close.Click += new System.EventHandler(this._close_Click);
            //
            // _apply
            //
            this._apply.Location = new System.Drawing.Point(343, 9);
            this._apply.Name = "_apply";
            this._apply.Size = new System.Drawing.Size(90, 23);
            this._apply.TabIndex = 1;
            this._apply.Text = "Apply";
            this._apply.Click += new System.EventHandler(this._apply_Click);
            //
            // _check
            //
            this._check.Location = new System.Drawing.Point(247, 9);
            this._check.Name = "_check";
            this._check.Size = new System.Drawing.Size(90, 23);
            this._check.TabIndex = 2;
            this._check.Text = "Check";
            this._check.Visible = false;
            this._check.Click += new System.EventHandler(this._check_Click);
            //
            // _status
            //
            this._status.AutoSize = true;
            this._status.Location = new System.Drawing.Point(337, 6);
            this._status.Name = "_status";
            this._status.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this._status.Size = new System.Drawing.Size(0, 19);
            this._status.TabIndex = 3;
            //
            // _rows
            //
            this._rows.AutoSize = true;
            this._rows.ColumnCount = 2;
            this._rows.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 55F));
            this._rows.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 45F));
            this._rows.Dock = System.Windows.Forms.DockStyle.Top;
            this._rows.Location = new System.Drawing.Point(0, 48);
            this._rows.Name = "_rows";
            this._rows.Padding = new System.Windows.Forms.Padding(8);
            this._rows.Size = new System.Drawing.Size(544, 16);
            this._rows.TabIndex = 2;
            //
            // introLabel
            //
            this.introLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this.introLabel.Location = new System.Drawing.Point(0, 0);
            this.introLabel.Name = "introLabel";
            this.introLabel.Padding = new System.Windows.Forms.Padding(8, 8, 8, 0);
            this.introLabel.Size = new System.Drawing.Size(544, 48);
            this.introLabel.TabIndex = 3;
            //
            // PathToolDialog
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(544, 601);
            this.Controls.Add(this.reportPanel);
            this.Controls.Add(this.buttonsPanel);
            this.Controls.Add(this._rows);
            this.Controls.Add(this.introLabel);
            this.MinimumSize = new System.Drawing.Size(460, 420);
            this.Name = "PathToolDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Shown += new System.EventHandler(this.PathToolDialog_Shown);
            this.reportPanel.ResumeLayout(false);
            this.reportPanel.PerformLayout();
            this.buttonsPanel.ResumeLayout(false);
            this.buttonsPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel reportPanel;
        private System.Windows.Forms.TextBox _report;
        private System.Windows.Forms.FlowLayoutPanel buttonsPanel;
        private System.Windows.Forms.Button _close;
        private System.Windows.Forms.Button _apply;
        private System.Windows.Forms.Button _check;
        private System.Windows.Forms.Label _status;
        private System.Windows.Forms.TableLayoutPanel _rows;
        private System.Windows.Forms.Label introLabel;
    }
}

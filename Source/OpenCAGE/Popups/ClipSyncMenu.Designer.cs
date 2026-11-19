namespace OpenCAGE.Popups
{
    partial class ClipSyncMenu
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
            this._notes = new System.Windows.Forms.Label();
            this.rowsTable = new System.Windows.Forms.TableLayoutPanel();
            this.cageLabel = new System.Windows.Forms.Label();
            this._cageBox = new System.Windows.Forms.ComboBox();
            this.offsetLabel = new System.Windows.Forms.Label();
            this._offset = new System.Windows.Forms.NumericUpDown();
            this.introLabel = new System.Windows.Forms.Label();
            this.buttonsPanel = new System.Windows.Forms.FlowLayoutPanel();
            this._cancel = new System.Windows.Forms.Button();
            this._unsync = new System.Windows.Forms.Button();
            this._sync = new System.Windows.Forms.Button();
            this.rowsTable.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._offset)).BeginInit();
            this.buttonsPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // _notes
            //
            this._notes.Dock = System.Windows.Forms.DockStyle.Fill;
            this._notes.Location = new System.Drawing.Point(10, 111);
            this._notes.Name = "_notes";
            this._notes.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this._notes.Size = new System.Drawing.Size(500, 180);
            this._notes.TabIndex = 0;
            //
            // rowsTable
            //
            this.rowsTable.AutoSize = true;
            this.rowsTable.ColumnCount = 2;
            this.rowsTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.rowsTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.rowsTable.Controls.Add(this.cageLabel, 0, 0);
            this.rowsTable.Controls.Add(this._cageBox, 1, 0);
            this.rowsTable.Controls.Add(this.offsetLabel, 0, 1);
            this.rowsTable.Controls.Add(this._offset, 1, 1);
            this.rowsTable.Dock = System.Windows.Forms.DockStyle.Top;
            this.rowsTable.Location = new System.Drawing.Point(10, 58);
            this.rowsTable.Name = "rowsTable";
            this.rowsTable.RowCount = 2;
            this.rowsTable.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.rowsTable.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.rowsTable.Size = new System.Drawing.Size(500, 53);
            this.rowsTable.TabIndex = 1;
            //
            // cageLabel
            //
            this.cageLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.cageLabel.AutoSize = true;
            this.cageLabel.Location = new System.Drawing.Point(3, 7);
            this.cageLabel.Name = "cageLabel";
            this.cageLabel.Size = new System.Drawing.Size(82, 13);
            this.cageLabel.TabIndex = 0;
            this.cageLabel.Text = "CAGEAnimation";
            //
            // _cageBox
            //
            this._cageBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._cageBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._cageBox.Location = new System.Drawing.Point(91, 3);
            this._cageBox.Name = "_cageBox";
            this._cageBox.Size = new System.Drawing.Size(406, 21);
            this._cageBox.TabIndex = 1;
            this._cageBox.SelectedIndexChanged += new System.EventHandler(this._cageBox_SelectedIndexChanged);
            //
            // offsetLabel
            //
            this.offsetLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.offsetLabel.AutoSize = true;
            this.offsetLabel.Location = new System.Drawing.Point(3, 33);
            this.offsetLabel.Name = "offsetLabel";
            this.offsetLabel.Size = new System.Drawing.Size(78, 13);
            this.offsetLabel.TabIndex = 2;
            this.offsetLabel.Text = "Clip starts at (s)";
            //
            // _offset
            //
            this._offset.DecimalPlaces = 3;
            this._offset.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this._offset.Location = new System.Drawing.Point(91, 30);
            this._offset.Maximum = new decimal(new int[] {
            36000,
            0,
            0,
            0});
            this._offset.Name = "_offset";
            this._offset.Size = new System.Drawing.Size(90, 20);
            this._offset.TabIndex = 3;
            this._offset.ValueChanged += new System.EventHandler(this._offset_ValueChanged);
            //
            // introLabel
            //
            this.introLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this.introLabel.Location = new System.Drawing.Point(10, 10);
            this.introLabel.Name = "introLabel";
            this.introLabel.Size = new System.Drawing.Size(500, 48);
            this.introLabel.TabIndex = 2;
            //
            // buttonsPanel
            //
            this.buttonsPanel.AutoSize = true;
            this.buttonsPanel.Controls.Add(this._cancel);
            this.buttonsPanel.Controls.Add(this._unsync);
            this.buttonsPanel.Controls.Add(this._sync);
            this.buttonsPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.buttonsPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.buttonsPanel.Location = new System.Drawing.Point(10, 291);
            this.buttonsPanel.Name = "buttonsPanel";
            this.buttonsPanel.Size = new System.Drawing.Size(500, 29);
            this.buttonsPanel.TabIndex = 3;
            //
            // _cancel
            //
            this._cancel.AutoSize = true;
            this._cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._cancel.Location = new System.Drawing.Point(407, 3);
            this._cancel.Name = "_cancel";
            this._cancel.Size = new System.Drawing.Size(90, 23);
            this._cancel.TabIndex = 0;
            this._cancel.Text = "Cancel";
            this._cancel.Click += new System.EventHandler(this._cancel_Click);
            //
            // _unsync
            //
            this._unsync.AutoSize = true;
            this._unsync.Location = new System.Drawing.Point(311, 3);
            this._unsync.Name = "_unsync";
            this._unsync.Size = new System.Drawing.Size(90, 23);
            this._unsync.TabIndex = 1;
            this._unsync.Text = "Unsync";
            this._unsync.Click += new System.EventHandler(this._unsync_Click);
            //
            // _sync
            //
            this._sync.AutoSize = true;
            this._sync.Location = new System.Drawing.Point(215, 3);
            this._sync.Name = "_sync";
            this._sync.Size = new System.Drawing.Size(90, 23);
            this._sync.TabIndex = 2;
            this._sync.Text = "Sync";
            this._sync.Click += new System.EventHandler(this._sync_Click);
            //
            // ClipSyncMenu
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._cancel;
            this.ClientSize = new System.Drawing.Size(520, 330);
            this.Controls.Add(this._notes);
            this.Controls.Add(this.rowsTable);
            this.Controls.Add(this.introLabel);
            this.Controls.Add(this.buttonsPanel);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(420, 300);
            this.Name = "ClipSyncMenu";
            this.Padding = new System.Windows.Forms.Padding(10);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.rowsTable.ResumeLayout(false);
            this.rowsTable.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._offset)).EndInit();
            this.buttonsPanel.ResumeLayout(false);
            this.buttonsPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label _notes;
        private System.Windows.Forms.TableLayoutPanel rowsTable;
        private System.Windows.Forms.Label cageLabel;
        private System.Windows.Forms.ComboBox _cageBox;
        private System.Windows.Forms.Label offsetLabel;
        private System.Windows.Forms.NumericUpDown _offset;
        private System.Windows.Forms.Label introLabel;
        private System.Windows.Forms.FlowLayoutPanel buttonsPanel;
        private System.Windows.Forms.Button _cancel;
        private System.Windows.Forms.Button _unsync;
        private System.Windows.Forms.Button _sync;
    }
}

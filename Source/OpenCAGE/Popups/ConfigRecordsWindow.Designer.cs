namespace OpenCAGE
{
    partial class ConfigRecordsWindow
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
            this._root = new System.Windows.Forms.FlowLayoutPanel();
            this._topRow = new System.Windows.Forms.FlowLayoutPanel();
            this._scopeLabel = new System.Windows.Forms.Label();
            this._scope = new System.Windows.Forms.ComboBox();
            this._scan = new System.Windows.Forms.Button();
            this._spareOnly = new System.Windows.Forms.CheckBox();
            this._listsRow = new System.Windows.Forms.FlowLayoutPanel();
            this._records = new System.Windows.Forms.ListView();
            this._colRecord = new System.Windows.Forms.ColumnHeader();
            this._colBuiltOn = new System.Windows.Forms.ColumnHeader();
            this._colUses = new System.Windows.Forms.ColumnHeader();
            this._colNote = new System.Windows.Forms.ColumnHeader();
            this._detail = new System.Windows.Forms.TextBox();
            this._status = new System.Windows.Forms.Label();
            this._root.SuspendLayout();
            this._topRow.SuspendLayout();
            this._listsRow.SuspendLayout();
            this.SuspendLayout();
            //
            // _root
            //
            this._root.AutoSize = true;
            this._root.Controls.Add(this._topRow);
            this._root.Controls.Add(this._listsRow);
            this._root.Controls.Add(this._status);
            this._root.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this._root.Location = new System.Drawing.Point(0, 0);
            this._root.Name = "_root";
            this._root.Padding = new System.Windows.Forms.Padding(10);
            this._root.TabIndex = 0;
            this._root.WrapContents = false;
            //
            // _topRow
            //
            this._topRow.AutoSize = true;
            this._topRow.Controls.Add(this._scopeLabel);
            this._topRow.Controls.Add(this._scope);
            this._topRow.Controls.Add(this._scan);
            this._topRow.Controls.Add(this._spareOnly);
            this._topRow.Name = "_topRow";
            this._topRow.TabIndex = 1;
            this._topRow.WrapContents = false;
            //
            // _scopeLabel
            //
            this._scopeLabel.AutoSize = true;
            this._scopeLabel.Name = "_scopeLabel";
            this._scopeLabel.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this._scopeLabel.TabIndex = 0;
            this._scopeLabel.Text = "Look in:";
            //
            // _scope
            //
            this._scope.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._scope.Items.AddRange(new object[] {
            "Every level on disk",
            "The game\'s own levels",
            "The open level only"});
            this._scope.Name = "_scope";
            this._scope.Size = new System.Drawing.Size(230, 21);
            this._scope.TabIndex = 1;
            //
            // _scan
            //
            this._scan.AutoSize = true;
            this._scan.Name = "_scan";
            this._scan.TabIndex = 2;
            this._scan.Text = "Find Uses";
            this._scan.Click += new System.EventHandler(this._scan_Click);
            //
            // _spareOnly
            //
            this._spareOnly.AutoSize = true;
            this._spareOnly.Name = "_spareOnly";
            this._spareOnly.TabIndex = 3;
            this._spareOnly.Text = "Spare records only";
            this._spareOnly.CheckedChanged += new System.EventHandler(this._spareOnly_CheckedChanged);
            //
            // _listsRow
            //
            this._listsRow.AutoSize = true;
            this._listsRow.Controls.Add(this._records);
            this._listsRow.Controls.Add(this._detail);
            this._listsRow.Name = "_listsRow";
            this._listsRow.TabIndex = 2;
            this._listsRow.WrapContents = false;
            //
            // _records
            //
            this._records.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colRecord,
            this._colBuiltOn,
            this._colUses,
            this._colNote});
            this._records.FullRowSelect = true;
            this._records.HideSelection = false;
            this._records.MultiSelect = false;
            this._records.Name = "_records";
            this._records.Size = new System.Drawing.Size(470, 330);
            this._records.TabIndex = 0;
            this._records.View = System.Windows.Forms.View.Details;
            this._records.SelectedIndexChanged += new System.EventHandler(this._records_SelectedIndexChanged);
            //
            // _colRecord
            //
            this._colRecord.Text = "Record";
            this._colRecord.Width = 170;
            //
            // _colBuiltOn
            //
            this._colBuiltOn.Text = "Built on";
            this._colBuiltOn.Width = 110;
            //
            // _colUses
            //
            this._colUses.Text = "Uses";
            this._colUses.Width = 60;
            //
            // _colNote
            //
            this._colNote.Text = "";
            this._colNote.Width = 110;
            //
            // _detail
            //
            this._detail.Multiline = true;
            this._detail.Name = "_detail";
            this._detail.ReadOnly = true;
            this._detail.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._detail.Size = new System.Drawing.Size(470, 330);
            this._detail.TabIndex = 1;
            //
            // _status
            //
            this._status.AutoSize = true;
            this._status.MaximumSize = new System.Drawing.Size(940, 0);
            this._status.Name = "_status";
            this._status.TabIndex = 3;
            //
            // ConfigRecordsWindow
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(975, 450);
            this.Controls.Add(this._root);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Name = "ConfigRecordsWindow";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ConfigRecordsWindow_FormClosing);
            this.Shown += new System.EventHandler(this.ConfigRecordsWindow_Shown);
            this._root.ResumeLayout(false);
            this._root.PerformLayout();
            this._topRow.ResumeLayout(false);
            this._topRow.PerformLayout();
            this._listsRow.ResumeLayout(false);
            this._listsRow.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel _root;
        private System.Windows.Forms.FlowLayoutPanel _topRow;
        private System.Windows.Forms.Label _scopeLabel;
        private System.Windows.Forms.ComboBox _scope;
        private System.Windows.Forms.Button _scan;
        private System.Windows.Forms.CheckBox _spareOnly;
        private System.Windows.Forms.FlowLayoutPanel _listsRow;
        private System.Windows.Forms.ListView _records;
        private System.Windows.Forms.ColumnHeader _colRecord;
        private System.Windows.Forms.ColumnHeader _colBuiltOn;
        private System.Windows.Forms.ColumnHeader _colUses;
        private System.Windows.Forms.ColumnHeader _colNote;
        private System.Windows.Forms.TextBox _detail;
        private System.Windows.Forms.Label _status;
    }
}

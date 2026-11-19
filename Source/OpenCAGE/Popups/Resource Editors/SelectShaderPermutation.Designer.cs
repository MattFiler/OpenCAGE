namespace OpenCAGE
{
    partial class SelectShaderPermutation
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
            this.components = new System.ComponentModel.Container();
            this._intro = new System.Windows.Forms.Label();
            this._filterLabel = new System.Windows.Forms.Label();
            this._filterPanel = new System.Windows.Forms.Panel();
            this._clear = new System.Windows.Forms.Button();
            this._matchCurrent = new System.Windows.Forms.Button();
            this._list = new System.Windows.Forms.ListView();
            this._featuresColumn = new System.Windows.Forms.ColumnHeader();
            this._maskColumn = new System.Windows.Forms.ColumnHeader();
            this._sourceColumn = new System.Windows.Forms.ColumnHeader();
            this._usedByColumn = new System.Windows.Forms.ColumnHeader();
            this._needsColumn = new System.Windows.Forms.ColumnHeader();
            this._status = new System.Windows.Forms.Label();
            this._ok = new System.Windows.Forms.Button();
            this._cancel = new System.Windows.Forms.Button();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            //
            // _intro
            //
            this._intro.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this._intro.ForeColor = System.Drawing.SystemColors.GrayText;
            this._intro.Location = new System.Drawing.Point(12, 10);
            this._intro.Name = "_intro";
            this._intro.Size = new System.Drawing.Size(876, 32);
            this._intro.TabIndex = 0;
            //
            // _filterLabel
            //
            this._filterLabel.Location = new System.Drawing.Point(12, 48);
            this._filterLabel.Name = "_filterLabel";
            this._filterLabel.Size = new System.Drawing.Size(280, 18);
            this._filterLabel.TabIndex = 1;
            this._filterLabel.Text = "Features wanted  (right click = don\'t care)";
            //
            // _filterPanel
            //
            this._filterPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)));
            this._filterPanel.AutoScroll = true;
            this._filterPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this._filterPanel.Location = new System.Drawing.Point(12, 68);
            this._filterPanel.Name = "_filterPanel";
            this._filterPanel.Size = new System.Drawing.Size(288, 452);
            this._filterPanel.TabIndex = 2;
            //
            // _clear
            //
            this._clear.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this._clear.Location = new System.Drawing.Point(12, 526);
            this._clear.Name = "_clear";
            this._clear.Size = new System.Drawing.Size(92, 26);
            this._clear.TabIndex = 3;
            this._clear.Text = "Clear filters";
            this._clear.Click += new System.EventHandler(this._clear_Click);
            //
            // _matchCurrent
            //
            this._matchCurrent.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this._matchCurrent.Location = new System.Drawing.Point(110, 526);
            this._matchCurrent.Name = "_matchCurrent";
            this._matchCurrent.Size = new System.Drawing.Size(100, 26);
            this._matchCurrent.TabIndex = 4;
            this._matchCurrent.Text = "Match current";
            this._matchCurrent.Click += new System.EventHandler(this._matchCurrent_Click);
            //
            // _list
            //
            this._list.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this._list.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._featuresColumn,
            this._maskColumn,
            this._sourceColumn,
            this._usedByColumn,
            this._needsColumn});
            this._list.FullRowSelect = true;
            this._list.HideSelection = false;
            this._list.Location = new System.Drawing.Point(308, 68);
            this._list.MultiSelect = false;
            this._list.Name = "_list";
            this._list.Size = new System.Drawing.Size(580, 452);
            this._list.TabIndex = 5;
            this._list.View = System.Windows.Forms.View.Details;
            this._list.SelectedIndexChanged += new System.EventHandler(this._list_SelectedIndexChanged);
            this._list.DoubleClick += new System.EventHandler(this._list_DoubleClick);
            //
            // _featuresColumn
            //
            this._featuresColumn.Text = "Features";
            this._featuresColumn.Width = 270;
            //
            // _maskColumn
            //
            this._maskColumn.Text = "Mask";
            this._maskColumn.Width = 90;
            //
            // _sourceColumn
            //
            this._sourceColumn.Text = "Source";
            this._sourceColumn.Width = 80;
            //
            // _usedByColumn
            //
            this._usedByColumn.Text = "Used by";
            this._usedByColumn.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this._usedByColumn.Width = 55;
            //
            // _needsColumn
            //
            this._needsColumn.Text = "Needs";
            this._needsColumn.Width = 80;
            //
            // _status
            //
            this._status.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this._status.ForeColor = System.Drawing.SystemColors.GrayText;
            this._status.Location = new System.Drawing.Point(308, 526);
            this._status.Name = "_status";
            this._status.Size = new System.Drawing.Size(400, 30);
            this._status.TabIndex = 6;
            //
            // _ok
            //
            this._ok.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._ok.DialogResult = System.Windows.Forms.DialogResult.OK;
            this._ok.Location = new System.Drawing.Point(732, 526);
            this._ok.Name = "_ok";
            this._ok.Size = new System.Drawing.Size(75, 26);
            this._ok.TabIndex = 7;
            this._ok.Text = "Use this";
            //
            // _cancel
            //
            this._cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._cancel.Location = new System.Drawing.Point(813, 526);
            this._cancel.Name = "_cancel";
            this._cancel.Size = new System.Drawing.Size(75, 26);
            this._cancel.TabIndex = 8;
            this._cancel.Text = "Cancel";
            //
            // SelectShaderPermutation
            //
            this.AcceptButton = this._ok;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._cancel;
            this.ClientSize = new System.Drawing.Size(900, 580);
            this.Controls.Add(this._intro);
            this.Controls.Add(this._filterLabel);
            this.Controls.Add(this._filterPanel);
            this.Controls.Add(this._clear);
            this.Controls.Add(this._matchCurrent);
            this.Controls.Add(this._list);
            this.Controls.Add(this._status);
            this.Controls.Add(this._ok);
            this.Controls.Add(this._cancel);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(720, 420);
            this.Name = "SelectShaderPermutation";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Choose a feature combination";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label _intro;
        private System.Windows.Forms.Label _filterLabel;
        private System.Windows.Forms.Panel _filterPanel;
        private System.Windows.Forms.Button _clear;
        private System.Windows.Forms.Button _matchCurrent;
        private System.Windows.Forms.ListView _list;
        private System.Windows.Forms.ColumnHeader _featuresColumn;
        private System.Windows.Forms.ColumnHeader _maskColumn;
        private System.Windows.Forms.ColumnHeader _sourceColumn;
        private System.Windows.Forms.ColumnHeader _usedByColumn;
        private System.Windows.Forms.ColumnHeader _needsColumn;
        private System.Windows.Forms.Label _status;
        private System.Windows.Forms.Button _ok;
        private System.Windows.Forms.Button _cancel;
        private System.Windows.Forms.ToolTip toolTip;
    }
}

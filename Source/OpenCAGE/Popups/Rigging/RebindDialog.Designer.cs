namespace OpenCAGE.Popups.Rigging
{
    partial class RebindDialog
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
            this._split = new System.Windows.Forms.SplitContainer();
            this._grid = new System.Windows.Forms.DataGridView();
            this._partColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._rigBoneColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._jointColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this._foundByColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._right = new System.Windows.Forms.SplitContainer();
            this._picture = new System.Windows.Forms.PictureBox();
            this._report = new System.Windows.Forms.TextBox();
            this._buttons = new System.Windows.Forms.FlowLayoutPanel();
            this._cancel = new System.Windows.Forms.Button();
            this._ok = new System.Windows.Forms.Button();
            this._reset = new System.Windows.Forms.Button();
            this._save = new System.Windows.Forms.Button();
            this._load = new System.Windows.Forms.Button();
            this._top = new System.Windows.Forms.FlowLayoutPanel();
            this._rigLabel = new System.Windows.Forms.Label();
            this._rig = new System.Windows.Forms.ComboBox();
            this._spacerLabel = new System.Windows.Forms.Label();
            this._fitSize = new System.Windows.Forms.CheckBox();
            this._scaleLabel = new System.Windows.Forms.Label();
            this._scale = new System.Windows.Forms.NumericUpDown();
            this._keepHead = new System.Windows.Forms.CheckBox();
            this._headHeight = new System.Windows.Forms.CheckBox();
            this._stretchTail = new System.Windows.Forms.CheckBox();
            this._hint = new System.Windows.Forms.Label();
            this._debounce = new System.Windows.Forms.Timer(this.components);
            ((System.ComponentModel.ISupportInitialize)(this._split)).BeginInit();
            this._split.Panel1.SuspendLayout();
            this._split.Panel2.SuspendLayout();
            this._split.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._grid)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._right)).BeginInit();
            this._right.Panel1.SuspendLayout();
            this._right.Panel2.SuspendLayout();
            this._right.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._picture)).BeginInit();
            this._buttons.SuspendLayout();
            this._top.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._scale)).BeginInit();
            this.SuspendLayout();
            //
            // _split
            //
            this._split.Dock = System.Windows.Forms.DockStyle.Fill;
            this._split.Location = new System.Drawing.Point(0, 64);
            this._split.Name = "_split";
            //
            // _split.Panel1
            //
            this._split.Panel1.Controls.Add(this._grid);
            //
            // _split.Panel2
            //
            this._split.Panel2.Controls.Add(this._right);
            this._split.Size = new System.Drawing.Size(1264, 717);
            this._split.SplitterDistance = 505;
            this._split.TabIndex = 0;
            //
            // _grid
            //
            this._grid.AllowUserToAddRows = false;
            this._grid.AllowUserToDeleteRows = false;
            this._grid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this._grid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this._partColumn,
            this._rigBoneColumn,
            this._jointColumn,
            this._foundByColumn});
            this._grid.Dock = System.Windows.Forms.DockStyle.Fill;
            this._grid.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this._grid.Location = new System.Drawing.Point(0, 0);
            this._grid.Name = "_grid";
            this._grid.RowHeadersVisible = false;
            this._grid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this._grid.Size = new System.Drawing.Size(505, 717);
            this._grid.TabIndex = 0;
            this._grid.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.GridValueChanged);
            this._grid.CurrentCellDirtyStateChanged += new System.EventHandler(this.Grid_CurrentCellDirtyStateChanged);
            this._grid.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.Grid_DataError);
            //
            // _partColumn
            //
            this._partColumn.FillWeight = 34F;
            this._partColumn.HeaderText = "Part";
            this._partColumn.Name = "_partColumn";
            this._partColumn.ReadOnly = true;
            //
            // _rigBoneColumn
            //
            this._rigBoneColumn.FillWeight = 22F;
            this._rigBoneColumn.HeaderText = "Rig bone";
            this._rigBoneColumn.Name = "_rigBoneColumn";
            this._rigBoneColumn.ReadOnly = true;
            //
            // _jointColumn
            //
            this._jointColumn.DisplayStyle = System.Windows.Forms.DataGridViewComboBoxDisplayStyle.DropDownButton;
            this._jointColumn.FillWeight = 30F;
            this._jointColumn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._jointColumn.HeaderText = "Joint in the file";
            this._jointColumn.Name = "_jointColumn";
            //
            // _foundByColumn
            //
            this._foundByColumn.FillWeight = 14F;
            this._foundByColumn.HeaderText = "Found by";
            this._foundByColumn.Name = "_foundByColumn";
            this._foundByColumn.ReadOnly = true;
            //
            // _right
            //
            this._right.Dock = System.Windows.Forms.DockStyle.Fill;
            this._right.Location = new System.Drawing.Point(0, 0);
            this._right.Name = "_right";
            this._right.Orientation = System.Windows.Forms.Orientation.Horizontal;
            //
            // _right.Panel1
            //
            this._right.Panel1.Controls.Add(this._picture);
            //
            // _right.Panel2
            //
            this._right.Panel2.Controls.Add(this._report);
            this._right.Size = new System.Drawing.Size(755, 717);
            this._right.SplitterDistance = 430;
            this._right.TabIndex = 0;
            //
            // _picture
            //
            this._picture.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this._picture.Dock = System.Windows.Forms.DockStyle.Fill;
            this._picture.Location = new System.Drawing.Point(0, 0);
            this._picture.Name = "_picture";
            this._picture.Size = new System.Drawing.Size(755, 430);
            this._picture.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this._picture.TabIndex = 0;
            this._picture.TabStop = false;
            //
            // _report
            //
            this._report.Dock = System.Windows.Forms.DockStyle.Fill;
            this._report.Location = new System.Drawing.Point(0, 0);
            this._report.Multiline = true;
            this._report.Name = "_report";
            this._report.ReadOnly = true;
            this._report.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._report.Size = new System.Drawing.Size(755, 283);
            this._report.TabIndex = 0;
            //
            // _buttons
            //
            this._buttons.Controls.Add(this._cancel);
            this._buttons.Controls.Add(this._ok);
            this._buttons.Controls.Add(this._reset);
            this._buttons.Controls.Add(this._save);
            this._buttons.Controls.Add(this._load);
            this._buttons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this._buttons.Location = new System.Drawing.Point(0, 781);
            this._buttons.Name = "_buttons";
            this._buttons.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);
            this._buttons.Size = new System.Drawing.Size(1264, 40);
            this._buttons.TabIndex = 1;
            //
            // _cancel
            //
            this._cancel.AutoSize = true;
            this._cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._cancel.Location = new System.Drawing.Point(1170, 7);
            this._cancel.Name = "_cancel";
            this._cancel.Size = new System.Drawing.Size(75, 23);
            this._cancel.TabIndex = 0;
            this._cancel.Text = "Cancel";
            //
            // _ok
            //
            this._ok.AutoSize = true;
            this._ok.Location = new System.Drawing.Point(1089, 7);
            this._ok.Name = "_ok";
            this._ok.Size = new System.Drawing.Size(75, 23);
            this._ok.TabIndex = 1;
            this._ok.Text = "Rebind";
            this._ok.Click += new System.EventHandler(this.Ok_Click);
            //
            // _reset
            //
            this._reset.AutoSize = true;
            this._reset.Location = new System.Drawing.Point(995, 7);
            this._reset.Name = "_reset";
            this._reset.Size = new System.Drawing.Size(88, 23);
            this._reset.TabIndex = 2;
            this._reset.Text = "Reset mapping";
            this._reset.Click += new System.EventHandler(this.Reset_Click);
            //
            // _save
            //
            this._save.AutoSize = true;
            this._save.Location = new System.Drawing.Point(895, 7);
            this._save.Name = "_save";
            this._save.Size = new System.Drawing.Size(94, 23);
            this._save.TabIndex = 3;
            this._save.Text = "Save mapping...";
            this._save.Click += new System.EventHandler(this.Save_Click);
            //
            // _load
            //
            this._load.AutoSize = true;
            this._load.Location = new System.Drawing.Point(796, 7);
            this._load.Name = "_load";
            this._load.Size = new System.Drawing.Size(93, 23);
            this._load.TabIndex = 4;
            this._load.Text = "Load mapping...";
            this._load.Click += new System.EventHandler(this.Load_Click);
            //
            // _top
            //
            this._top.Controls.Add(this._rigLabel);
            this._top.Controls.Add(this._rig);
            this._top.Controls.Add(this._spacerLabel);
            this._top.Controls.Add(this._fitSize);
            this._top.Controls.Add(this._scaleLabel);
            this._top.Controls.Add(this._scale);
            this._top.Controls.Add(this._keepHead);
            this._top.Controls.Add(this._headHeight);
            this._top.Controls.Add(this._stretchTail);
            this._top.Controls.Add(this._hint);
            this._top.Dock = System.Windows.Forms.DockStyle.Top;
            this._top.Location = new System.Drawing.Point(0, 0);
            this._top.Name = "_top";
            this._top.Padding = new System.Windows.Forms.Padding(8, 8, 8, 0);
            this._top.Size = new System.Drawing.Size(1264, 64);
            this._top.TabIndex = 2;
            //
            // _rigLabel
            //
            this._rigLabel.AutoSize = true;
            this._rigLabel.Location = new System.Drawing.Point(8, 14);
            this._rigLabel.Margin = new System.Windows.Forms.Padding(0, 6, 2, 0);
            this._rigLabel.Name = "_rigLabel";
            this._rigLabel.Size = new System.Drawing.Size(26, 13);
            this._rigLabel.TabIndex = 0;
            this._rigLabel.Text = "Rig:";
            //
            // _rig
            //
            this._rig.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._rig.Location = new System.Drawing.Point(39, 11);
            this._rig.Name = "_rig";
            this._rig.Size = new System.Drawing.Size(180, 21);
            this._rig.TabIndex = 1;
            //
            // _spacerLabel
            //
            this._spacerLabel.AutoSize = true;
            this._spacerLabel.Location = new System.Drawing.Point(225, 8);
            this._spacerLabel.Name = "_spacerLabel";
            this._spacerLabel.Size = new System.Drawing.Size(16, 13);
            this._spacerLabel.TabIndex = 2;
            this._spacerLabel.Text = "   ";
            //
            // _fitSize
            //
            this._fitSize.AutoSize = true;
            this._fitSize.Checked = true;
            this._fitSize.CheckState = System.Windows.Forms.CheckState.Checked;
            this._fitSize.Location = new System.Drawing.Point(247, 11);
            this._fitSize.Name = "_fitSize";
            this._fitSize.Size = new System.Drawing.Size(98, 17);
            this._fitSize.TabIndex = 3;
            this._fitSize.Text = "Size it to the rig";
            this._fitSize.CheckedChanged += new System.EventHandler(this.Option_Changed);
            //
            // _scaleLabel
            //
            this._scaleLabel.AutoSize = true;
            this._scaleLabel.Location = new System.Drawing.Point(348, 14);
            this._scaleLabel.Margin = new System.Windows.Forms.Padding(0, 6, 2, 0);
            this._scaleLabel.Name = "_scaleLabel";
            this._scaleLabel.Size = new System.Drawing.Size(36, 13);
            this._scaleLabel.TabIndex = 4;
            this._scaleLabel.Text = "then x";
            //
            // _scale
            //
            this._scale.DecimalPlaces = 3;
            this._scale.Increment = new decimal(new int[] {
            5,
            0,
            0,
            131072});
            this._scale.Location = new System.Drawing.Point(389, 11);
            this._scale.Maximum = new decimal(new int[] {
            20,
            0,
            0,
            0});
            this._scale.Minimum = new decimal(new int[] {
            5,
            0,
            0,
            131072});
            this._scale.Name = "_scale";
            this._scale.Size = new System.Drawing.Size(70, 20);
            this._scale.TabIndex = 5;
            this._scale.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this._scale.ValueChanged += new System.EventHandler(this.Option_Changed);
            //
            // _keepHead
            //
            this._keepHead.AutoSize = true;
            this._keepHead.Checked = true;
            this._keepHead.CheckState = System.Windows.Forms.CheckState.Checked;
            this._keepHead.Location = new System.Drawing.Point(465, 11);
            this._keepHead.Name = "_keepHead";
            this._keepHead.Size = new System.Drawing.Size(135, 17);
            this._keepHead.TabIndex = 6;
            this._keepHead.Text = "Keep the head\'s shape";
            this._keepHead.CheckedChanged += new System.EventHandler(this.Option_Changed);
            //
            // _headHeight
            //
            this._headHeight.AutoSize = true;
            this._headHeight.Checked = true;
            this._headHeight.CheckState = System.Windows.Forms.CheckState.Checked;
            this._headHeight.Location = new System.Drawing.Point(606, 11);
            this._headHeight.Name = "_headHeight";
            this._headHeight.Size = new System.Drawing.Size(248, 17);
            this._headHeight.TabIndex = 7;
            this._headHeight.Text = "Keep the head\'s own height over the shoulders";
            this._headHeight.CheckedChanged += new System.EventHandler(this.Option_Changed);
            //
            // _stretchTail
            //
            this._stretchTail.AutoSize = true;
            this._stretchTail.Location = new System.Drawing.Point(860, 11);
            this._stretchTail.Name = "_stretchTail";
            this._stretchTail.Size = new System.Drawing.Size(168, 17);
            this._stretchTail.TabIndex = 8;
            this._stretchTail.Text = "Stretch a tail to the rig\'s length";
            this._stretchTail.CheckedChanged += new System.EventHandler(this.Option_Changed);
            //
            // _hint
            //
            this._hint.AutoSize = true;
            this._hint.ForeColor = System.Drawing.SystemColors.GrayText;
            this._hint.Location = new System.Drawing.Point(11, 35);
            this._hint.MaximumSize = new System.Drawing.Size(1200, 0);
            this._hint.Name = "_hint";
            this._hint.Size = new System.Drawing.Size(1007, 13);
            this._hint.TabIndex = 9;
            this._hint.Text = "Each part of the body is a joint of the file, found from its name and the hierarchy: pick another in the right-hand column to fix one, or (none) to leave it out. The spine, neck and tail follow the joints between the parts.";
            //
            // _debounce
            //
            this._debounce.Interval = 250;
            this._debounce.Tick += new System.EventHandler(this.Debounce_Tick);
            //
            // RebindDialog
            //
            this.AcceptButton = this._ok;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._cancel;
            this.ClientSize = new System.Drawing.Size(1264, 821);
            this.Controls.Add(this._split);
            this.Controls.Add(this._buttons);
            this.Controls.Add(this._top);
            this.MinimumSize = new System.Drawing.Size(980, 640);
            this.Name = "RebindDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Rebind onto a game skeleton";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.RebindDialog_FormClosed);
            this.Shown += new System.EventHandler(this.RebindDialog_Shown);
            this._split.Panel1.ResumeLayout(false);
            this._split.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._split)).EndInit();
            this._split.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._grid)).EndInit();
            this._right.Panel1.ResumeLayout(false);
            this._right.Panel2.ResumeLayout(false);
            this._right.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._right)).EndInit();
            this._right.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._picture)).EndInit();
            this._buttons.ResumeLayout(false);
            this._buttons.PerformLayout();
            this._top.ResumeLayout(false);
            this._top.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._scale)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer _split;
        private System.Windows.Forms.DataGridView _grid;
        private System.Windows.Forms.DataGridViewTextBoxColumn _partColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn _rigBoneColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn _jointColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn _foundByColumn;
        private System.Windows.Forms.SplitContainer _right;
        private System.Windows.Forms.PictureBox _picture;
        private System.Windows.Forms.TextBox _report;
        private System.Windows.Forms.FlowLayoutPanel _buttons;
        private System.Windows.Forms.Button _cancel;
        private System.Windows.Forms.Button _ok;
        private System.Windows.Forms.Button _reset;
        private System.Windows.Forms.Button _save;
        private System.Windows.Forms.Button _load;
        private System.Windows.Forms.FlowLayoutPanel _top;
        private System.Windows.Forms.Label _rigLabel;
        private System.Windows.Forms.ComboBox _rig;
        private System.Windows.Forms.Label _spacerLabel;
        private System.Windows.Forms.CheckBox _fitSize;
        private System.Windows.Forms.Label _scaleLabel;
        private System.Windows.Forms.NumericUpDown _scale;
        private System.Windows.Forms.CheckBox _keepHead;
        private System.Windows.Forms.CheckBox _headHeight;
        private System.Windows.Forms.CheckBox _stretchTail;
        private System.Windows.Forms.Label _hint;
        private System.Windows.Forms.Timer _debounce;
    }
}
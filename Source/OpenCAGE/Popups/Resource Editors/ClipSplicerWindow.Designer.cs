namespace OpenCAGE
{
    partial class ClipSplicerWindow
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
            this._middle = new System.Windows.Forms.Panel();
            this._grid = new System.Windows.Forms.DataGridView();
            this._clipColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._framesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._fromColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._toColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._speedColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._blendColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._holdColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._carryOnColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this._turnColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._carryForwardColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._rowButtons = new System.Windows.Forms.FlowLayoutPanel();
            this._addBtn = new System.Windows.Forms.Button();
            this._upBtn = new System.Windows.Forms.Button();
            this._downBtn = new System.Windows.Forms.Button();
            this._removeBtn = new System.Windows.Forms.Button();
            this._bottom = new System.Windows.Forms.Panel();
            this._report = new System.Windows.Forms.TextBox();
            this._timelineHost = new System.Windows.Forms.Panel();
            this._actions = new System.Windows.Forms.FlowLayoutPanel();
            this._saveBtn = new System.Windows.Forms.Button();
            this._previewBtn = new System.Windows.Forms.Button();
            this._checkBtn = new System.Windows.Forms.Button();
            this._top = new System.Windows.Forms.TableLayoutPanel();
            this._setLabel = new System.Windows.Forms.Label();
            this._setBox = new System.Windows.Forms.ComboBox();
            this._nameLabel = new System.Windows.Forms.Label();
            this._nameBox = new System.Windows.Forms.TextBox();
            this._contextLabel = new System.Windows.Forms.Label();
            this._contextBox = new System.Windows.Forms.ComboBox();
            this._travel = new System.Windows.Forms.CheckBox();
            this._fromStart = new System.Windows.Forms.CheckBox();
            this._lockFeet = new System.Windows.Forms.CheckBox();
            this._tips = new System.Windows.Forms.ToolTip(this.components);
            this._middle.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._grid)).BeginInit();
            this._rowButtons.SuspendLayout();
            this._bottom.SuspendLayout();
            this._actions.SuspendLayout();
            this._top.SuspendLayout();
            this.SuspendLayout();
            //
            // _middle
            //
            this._middle.Controls.Add(this._grid);
            this._middle.Controls.Add(this._rowButtons);
            this._middle.Dock = System.Windows.Forms.DockStyle.Fill;
            this._middle.Location = new System.Drawing.Point(0, 64);
            this._middle.Name = "_middle";
            this._middle.Size = new System.Drawing.Size(940, 286);
            this._middle.TabIndex = 0;
            //
            // _grid
            //
            this._grid.AllowUserToAddRows = false;
            this._grid.AllowUserToDeleteRows = false;
            this._grid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this._grid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this._clipColumn,
            this._framesColumn,
            this._fromColumn,
            this._toColumn,
            this._speedColumn,
            this._blendColumn,
            this._holdColumn,
            this._carryOnColumn,
            this._turnColumn,
            this._carryForwardColumn});
            this._grid.Dock = System.Windows.Forms.DockStyle.Fill;
            this._grid.Location = new System.Drawing.Point(0, 0);
            this._grid.MultiSelect = false;
            this._grid.Name = "_grid";
            this._grid.RowHeadersVisible = false;
            this._grid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this._grid.Size = new System.Drawing.Size(830, 286);
            this._grid.TabIndex = 0;
            this._grid.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this._grid_CellValueChanged);
            //
            // _clipColumn
            //
            this._clipColumn.FillWeight = 220F;
            this._clipColumn.HeaderText = "Clip";
            this._clipColumn.Name = "_clipColumn";
            this._clipColumn.ReadOnly = true;
            this._clipColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this._clipColumn.ToolTipText = "The clip and its set";
            //
            // _framesColumn
            //
            this._framesColumn.FillWeight = 80F;
            this._framesColumn.HeaderText = "Frames";
            this._framesColumn.Name = "_framesColumn";
            this._framesColumn.ReadOnly = true;
            this._framesColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this._framesColumn.ToolTipText = "Frames the clip has";
            //
            // _fromColumn
            //
            this._fromColumn.FillWeight = 50F;
            this._fromColumn.HeaderText = "From";
            this._fromColumn.Name = "_fromColumn";
            this._fromColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this._fromColumn.ToolTipText = "First frame to use (from 0)";
            //
            // _toColumn
            //
            this._toColumn.FillWeight = 50F;
            this._toColumn.HeaderText = "To";
            this._toColumn.Name = "_toColumn";
            this._toColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this._toColumn.ToolTipText = "Last frame to use, inclusive (-1 = its last)";
            //
            // _speedColumn
            //
            this._speedColumn.FillWeight = 50F;
            this._speedColumn.HeaderText = "Speed";
            this._speedColumn.Name = "_speedColumn";
            this._speedColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this._speedColumn.ToolTipText = "Playback speed: 0.5 is half speed (twice as long)";
            //
            // _blendColumn
            //
            this._blendColumn.FillWeight = 60F;
            this._blendColumn.HeaderText = "Blend (s)";
            this._blendColumn.Name = "_blendColumn";
            this._blendColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this._blendColumn.ToolTipText = "Seconds to crossfade in from the previous piece: the end of that one overlaps the start of this. 0 = a hard cut, right for clips made to follow one another";
            //
            // _holdColumn
            //
            this._holdColumn.FillWeight = 55F;
            this._holdColumn.HeaderText = "Hold (s)";
            this._holdColumn.Name = "_holdColumn";
            this._holdColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this._holdColumn.ToolTipText = "Seconds to hold this piece\'s last frame";
            //
            // _carryOnColumn
            //
            this._carryOnColumn.FillWeight = 55F;
            this._carryOnColumn.HeaderText = "Carry on";
            this._carryOnColumn.Name = "_carryOnColumn";
            this._carryOnColumn.ToolTipText = "Carry on from where the previous piece got to (off: keep this clip\'s own placement, for pieces of one scene)";
            //
            // _turnColumn
            //
            this._turnColumn.FillWeight = 60F;
            this._turnColumn.HeaderText = "Turn (deg)";
            this._turnColumn.Name = "_turnColumn";
            this._turnColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this._turnColumn.ToolTipText = "An extra turn at the join (degrees; positive turns +Z towards +X)";
            //
            // _carryForwardColumn
            //
            this._carryForwardColumn.FillWeight = 75F;
            this._carryForwardColumn.HeaderText = "Carry fwd (m)";
            this._carryForwardColumn.Name = "_carryForwardColumn";
            this._carryForwardColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this._carryForwardColumn.ToolTipText = "Extra travel forward over this piece: shipped leaps jump on the spot (the traversal code moves them), so a leap over a gap says how far";
            //
            // _rowButtons
            //
            this._rowButtons.Controls.Add(this._addBtn);
            this._rowButtons.Controls.Add(this._upBtn);
            this._rowButtons.Controls.Add(this._downBtn);
            this._rowButtons.Controls.Add(this._removeBtn);
            this._rowButtons.Dock = System.Windows.Forms.DockStyle.Right;
            this._rowButtons.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this._rowButtons.Location = new System.Drawing.Point(830, 0);
            this._rowButtons.Name = "_rowButtons";
            this._rowButtons.Padding = new System.Windows.Forms.Padding(4);
            this._rowButtons.Size = new System.Drawing.Size(110, 286);
            this._rowButtons.TabIndex = 1;
            //
            // _addBtn
            //
            this._addBtn.AutoSize = true;
            this._addBtn.Location = new System.Drawing.Point(7, 7);
            this._addBtn.Name = "_addBtn";
            this._addBtn.Size = new System.Drawing.Size(75, 23);
            this._addBtn.TabIndex = 0;
            this._addBtn.Text = "Add Clip...";
            this._addBtn.Click += new System.EventHandler(this._addBtn_Click);
            //
            // _upBtn
            //
            this._upBtn.AutoSize = true;
            this._upBtn.Location = new System.Drawing.Point(7, 36);
            this._upBtn.Name = "_upBtn";
            this._upBtn.Size = new System.Drawing.Size(75, 23);
            this._upBtn.TabIndex = 1;
            this._upBtn.Text = "Up";
            this._upBtn.Click += new System.EventHandler(this._upBtn_Click);
            //
            // _downBtn
            //
            this._downBtn.AutoSize = true;
            this._downBtn.Location = new System.Drawing.Point(7, 65);
            this._downBtn.Name = "_downBtn";
            this._downBtn.Size = new System.Drawing.Size(75, 23);
            this._downBtn.TabIndex = 2;
            this._downBtn.Text = "Down";
            this._downBtn.Click += new System.EventHandler(this._downBtn_Click);
            //
            // _removeBtn
            //
            this._removeBtn.AutoSize = true;
            this._removeBtn.Location = new System.Drawing.Point(7, 94);
            this._removeBtn.Name = "_removeBtn";
            this._removeBtn.Size = new System.Drawing.Size(75, 23);
            this._removeBtn.TabIndex = 3;
            this._removeBtn.Text = "Remove";
            this._removeBtn.Click += new System.EventHandler(this._removeBtn_Click);
            //
            // _bottom
            //
            this._bottom.Controls.Add(this._report);
            this._bottom.Controls.Add(this._timelineHost);
            this._bottom.Controls.Add(this._actions);
            this._bottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._bottom.Location = new System.Drawing.Point(0, 350);
            this._bottom.Name = "_bottom";
            this._bottom.Size = new System.Drawing.Size(940, 250);
            this._bottom.TabIndex = 1;
            //
            // _report
            //
            this._report.Dock = System.Windows.Forms.DockStyle.Fill;
            this._report.Font = new System.Drawing.Font("Courier New", 8.5F);
            this._report.Location = new System.Drawing.Point(0, 50);
            this._report.Multiline = true;
            this._report.Name = "_report";
            this._report.ReadOnly = true;
            this._report.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._report.Size = new System.Drawing.Size(940, 164);
            this._report.TabIndex = 0;
            //
            // _timelineHost
            //
            this._timelineHost.Dock = System.Windows.Forms.DockStyle.Top;
            this._timelineHost.Location = new System.Drawing.Point(0, 0);
            this._timelineHost.Name = "_timelineHost";
            this._timelineHost.Size = new System.Drawing.Size(940, 50);
            this._timelineHost.TabIndex = 1;
            //
            // _actions
            //
            this._actions.Controls.Add(this._saveBtn);
            this._actions.Controls.Add(this._previewBtn);
            this._actions.Controls.Add(this._checkBtn);
            this._actions.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._actions.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this._actions.Location = new System.Drawing.Point(0, 214);
            this._actions.Name = "_actions";
            this._actions.Padding = new System.Windows.Forms.Padding(4);
            this._actions.Size = new System.Drawing.Size(940, 36);
            this._actions.TabIndex = 2;
            //
            // _saveBtn
            //
            this._saveBtn.AutoSize = true;
            this._saveBtn.Location = new System.Drawing.Point(858, 7);
            this._saveBtn.Name = "_saveBtn";
            this._saveBtn.Size = new System.Drawing.Size(75, 23);
            this._saveBtn.TabIndex = 0;
            this._saveBtn.Text = "Save";
            this._tips.SetToolTip(this._saveBtn, "Add it to the set and write ANIMATION.PAK (shared by every level)");
            this._saveBtn.Click += new System.EventHandler(this._saveBtn_Click);
            //
            // _previewBtn
            //
            this._previewBtn.AutoSize = true;
            this._previewBtn.Location = new System.Drawing.Point(777, 7);
            this._previewBtn.Name = "_previewBtn";
            this._previewBtn.Size = new System.Drawing.Size(75, 23);
            this._previewBtn.TabIndex = 1;
            this._previewBtn.Text = "Preview";
            this._tips.SetToolTip(this._previewBtn, "Build it and play it in the Animation Preview (tick root motion to see it travel; the ring is the capsule)");
            this._previewBtn.Click += new System.EventHandler(this._previewBtn_Click);
            //
            // _checkBtn
            //
            this._checkBtn.AutoSize = true;
            this._checkBtn.Location = new System.Drawing.Point(696, 7);
            this._checkBtn.Name = "_checkBtn";
            this._checkBtn.Size = new System.Drawing.Size(75, 23);
            this._checkBtn.TabIndex = 2;
            this._checkBtn.Text = "Check";
            this._tips.SetToolTip(this._checkBtn, "Build it and report the joins, without saving");
            this._checkBtn.Click += new System.EventHandler(this._checkBtn_Click);
            //
            // _top
            //
            this._top.ColumnCount = 6;
            this._top.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._top.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._top.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._top.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._top.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._top.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._top.Controls.Add(this._setLabel, 0, 0);
            this._top.Controls.Add(this._setBox, 1, 0);
            this._top.Controls.Add(this._nameLabel, 2, 0);
            this._top.Controls.Add(this._nameBox, 3, 0);
            this._top.Controls.Add(this._contextLabel, 4, 0);
            this._top.Controls.Add(this._contextBox, 5, 0);
            this._top.Controls.Add(this._travel, 1, 1);
            this._top.Controls.Add(this._fromStart, 3, 1);
            this._top.Controls.Add(this._lockFeet, 5, 1);
            this._top.Dock = System.Windows.Forms.DockStyle.Top;
            this._top.Location = new System.Drawing.Point(0, 0);
            this._top.Name = "_top";
            this._top.Padding = new System.Windows.Forms.Padding(6);
            this._top.RowCount = 2;
            this._top.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._top.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._top.Size = new System.Drawing.Size(940, 64);
            this._top.TabIndex = 2;
            //
            // _setLabel
            //
            this._setLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._setLabel.AutoSize = true;
            this._setLabel.Location = new System.Drawing.Point(9, 13);
            this._setLabel.Name = "_setLabel";
            this._setLabel.Size = new System.Drawing.Size(43, 13);
            this._setLabel.TabIndex = 0;
            this._setLabel.Text = "Into set";
            //
            // _setBox
            //
            this._setBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._setBox.Location = new System.Drawing.Point(58, 9);
            this._setBox.Name = "_setBox";
            this._setBox.Size = new System.Drawing.Size(200, 21);
            this._setBox.TabIndex = 1;
            this._setBox.SelectedIndexChanged += new System.EventHandler(this._setBox_SelectedIndexChanged);
            //
            // _nameLabel
            //
            this._nameLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._nameLabel.AutoSize = true;
            this._nameLabel.Location = new System.Drawing.Point(264, 13);
            this._nameLabel.Name = "_nameLabel";
            this._nameLabel.Size = new System.Drawing.Size(35, 13);
            this._nameLabel.TabIndex = 2;
            this._nameLabel.Text = "Name";
            //
            // _nameBox
            //
            this._nameBox.Location = new System.Drawing.Point(305, 9);
            this._nameBox.Name = "_nameBox";
            this._nameBox.Size = new System.Drawing.Size(220, 20);
            this._nameBox.TabIndex = 3;
            this._nameBox.Text = "spliced_clip";
            //
            // _contextLabel
            //
            this._contextLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._contextLabel.AutoSize = true;
            this._contextLabel.Location = new System.Drawing.Point(531, 13);
            this._contextLabel.Name = "_contextLabel";
            this._contextLabel.Size = new System.Drawing.Size(43, 13);
            this._contextLabel.TabIndex = 4;
            this._contextLabel.Text = "Context";
            //
            // _contextBox
            //
            this._contextBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._contextBox.Location = new System.Drawing.Point(580, 9);
            this._contextBox.Name = "_contextBox";
            this._contextBox.Size = new System.Drawing.Size(200, 21);
            this._contextBox.TabIndex = 5;
            //
            // _travel
            //
            this._travel.AutoSize = true;
            this._travel.Checked = true;
            this._travel.CheckState = System.Windows.Forms.CheckState.Checked;
            this._travel.Location = new System.Drawing.Point(58, 36);
            this._travel.Name = "_travel";
            this._travel.Size = new System.Drawing.Size(101, 17);
            this._travel.TabIndex = 6;
            this._travel.Text = "Keep the travel";
            this._tips.SetToolTip(this._travel, "Keep the pieces\' travel: REFERENCE_ROOT carries it and the capsule follows the body (off: it all plays on the spot)");
            //
            // _fromStart
            //
            this._fromStart.AutoSize = true;
            this._fromStart.Checked = true;
            this._fromStart.CheckState = System.Windows.Forms.CheckState.Checked;
            this._fromStart.Location = new System.Drawing.Point(305, 36);
            this._fromStart.Name = "_fromStart";
            this._fromStart.Size = new System.Drawing.Size(119, 17);
            this._fromStart.TabIndex = 7;
            this._fromStart.Text = "Start on the marker";
            this._tips.SetToolTip(this._fromStart, "Start where the character stands / on the marker, facing along it (off: the first piece\'s own placement is the origin)");
            //
            // _lockFeet
            //
            this._lockFeet.AutoSize = true;
            this._lockFeet.Checked = true;
            this._lockFeet.CheckState = System.Windows.Forms.CheckState.Checked;
            this._lockFeet.Location = new System.Drawing.Point(580, 36);
            this._lockFeet.Name = "_lockFeet";
            this._lockFeet.Size = new System.Drawing.Size(109, 17);
            this._lockFeet.TabIndex = 8;
            this._lockFeet.Text = "Lock planted feet";
            this._tips.SetToolTip(this._lockFeet, "A foot planted on both sides of a join is put down on the same spot and held there through the crossfade until it lifts (two-bone IK), so a blend between two stances does not slide it");
            //
            // ClipSplicerWindow
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(940, 600);
            this.Controls.Add(this._middle);
            this.Controls.Add(this._bottom);
            this.Controls.Add(this._top);
            this.MinimumSize = new System.Drawing.Size(760, 480);
            this.Name = "ClipSplicerWindow";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Clip Splicer";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.ClipSplicerWindow_FormClosed);
            this._middle.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._grid)).EndInit();
            this._rowButtons.ResumeLayout(false);
            this._rowButtons.PerformLayout();
            this._bottom.ResumeLayout(false);
            this._bottom.PerformLayout();
            this._actions.ResumeLayout(false);
            this._actions.PerformLayout();
            this._top.ResumeLayout(false);
            this._top.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel _middle;
        private System.Windows.Forms.DataGridView _grid;
        private System.Windows.Forms.DataGridViewTextBoxColumn _clipColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn _framesColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn _fromColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn _toColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn _speedColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn _blendColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn _holdColumn;
        private System.Windows.Forms.DataGridViewCheckBoxColumn _carryOnColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn _turnColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn _carryForwardColumn;
        private System.Windows.Forms.FlowLayoutPanel _rowButtons;
        private System.Windows.Forms.Button _addBtn;
        private System.Windows.Forms.Button _upBtn;
        private System.Windows.Forms.Button _downBtn;
        private System.Windows.Forms.Button _removeBtn;
        private System.Windows.Forms.Panel _bottom;
        private System.Windows.Forms.TextBox _report;
        private System.Windows.Forms.Panel _timelineHost;
        private System.Windows.Forms.FlowLayoutPanel _actions;
        private System.Windows.Forms.Button _saveBtn;
        private System.Windows.Forms.Button _previewBtn;
        private System.Windows.Forms.Button _checkBtn;
        private System.Windows.Forms.TableLayoutPanel _top;
        private System.Windows.Forms.Label _setLabel;
        private System.Windows.Forms.ComboBox _setBox;
        private System.Windows.Forms.Label _nameLabel;
        private System.Windows.Forms.TextBox _nameBox;
        private System.Windows.Forms.Label _contextLabel;
        private System.Windows.Forms.ComboBox _contextBox;
        private System.Windows.Forms.CheckBox _travel;
        private System.Windows.Forms.CheckBox _fromStart;
        private System.Windows.Forms.CheckBox _lockFeet;
        private System.Windows.Forms.ToolTip _tips;
    }
}

namespace OpenCAGE.Popups
{
    partial class ZFightWindow
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
            this.rootPanel = new System.Windows.Forms.FlowLayoutPanel();
            this._title = new System.Windows.Forms.Label();
            this.introLabel = new System.Windows.Forms.Label();
            this.searchRow = new System.Windows.Forms.FlowLayoutPanel();
            this.searchLabel = new System.Windows.Forms.Label();
            this._scope = new System.Windows.Forms.ComboBox();
            this.withinLabel = new System.Windows.Forms.Label();
            this._radius = new System.Windows.Forms.NumericUpDown();
            this.ofItLabel = new System.Windows.Forms.Label();
            this.gapRow = new System.Windows.Forms.FlowLayoutPanel();
            this.closerLabel = new System.Windows.Forms.Label();
            this._gap = new System.Windows.Forms.NumericUpDown();
            this.mmWithinLabel = new System.Windows.Forms.Label();
            this._angle = new System.Windows.Forms.NumericUpDown();
            this.parallelLabel = new System.Windows.Forms.Label();
            this._minArea = new System.Windows.Forms.NumericUpDown();
            this.areaUnitLabel = new System.Windows.Forms.Label();
            this._find = new System.Windows.Forms.Button();
            this.optionsRow = new System.Windows.Forms.FlowLayoutPanel();
            this._inside = new System.Windows.Forms.CheckBox();
            this._hidden = new System.Windows.Forms.CheckBox();
            this._decals = new System.Windows.Forms.CheckBox();
            this._list = new System.Windows.Forms.ListView();
            this.numberColumn = new System.Windows.Forms.ColumnHeader();
            this.overlapColumn = new System.Windows.Forms.ColumnHeader();
            this.apartColumn = new System.Windows.Forms.ColumnHeader();
            this.onTopColumn = new System.Windows.Forms.ColumnHeader();
            this.underneathColumn = new System.Windows.Forms.ColumnHeader();
            this.kindColumn = new System.Windows.Forms.ColumnHeader();
            this._preview = new System.Windows.Forms.PictureBox();
            this.fixRow = new System.Windows.Forms.FlowLayoutPanel();
            this.fixLabel = new System.Windows.Forms.Label();
            this._action = new System.Windows.Forms.ComboBox();
            this.untilLabel = new System.Windows.Forms.Label();
            this._clearance = new System.Windows.Forms.NumericUpDown();
            this.mmApartLabel = new System.Windows.Forms.Label();
            this._fixSelected = new System.Windows.Forms.Button();
            this._fixAll = new System.Windows.Forms.Button();
            this._close = new System.Windows.Forms.Button();
            this._status = new System.Windows.Forms.Label();
            this._replan = new System.Windows.Forms.Timer(this.components);
            this.rootPanel.SuspendLayout();
            this.searchRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._radius)).BeginInit();
            this.gapRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._gap)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._angle)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._minArea)).BeginInit();
            this.optionsRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._preview)).BeginInit();
            this.fixRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._clearance)).BeginInit();
            this.SuspendLayout();
            //
            // rootPanel
            //
            this.rootPanel.AutoSize = true;
            this.rootPanel.Controls.Add(this._title);
            this.rootPanel.Controls.Add(this.introLabel);
            this.rootPanel.Controls.Add(this.searchRow);
            this.rootPanel.Controls.Add(this.gapRow);
            this.rootPanel.Controls.Add(this.optionsRow);
            this.rootPanel.Controls.Add(this._list);
            this.rootPanel.Controls.Add(this._preview);
            this.rootPanel.Controls.Add(this.fixRow);
            this.rootPanel.Controls.Add(this._status);
            this.rootPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.rootPanel.Location = new System.Drawing.Point(0, 0);
            this.rootPanel.Name = "rootPanel";
            this.rootPanel.Padding = new System.Windows.Forms.Padding(10);
            this.rootPanel.Size = new System.Drawing.Size(786, 765);
            this.rootPanel.TabIndex = 0;
            this.rootPanel.WrapContents = false;
            //
            // _title
            //
            this._title.AutoSize = true;
            this._title.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
            this._title.Location = new System.Drawing.Point(13, 10);
            this._title.MaximumSize = new System.Drawing.Size(760, 0);
            this._title.Name = "_title";
            this._title.Size = new System.Drawing.Size(0, 13);
            this._title.TabIndex = 0;
            //
            // introLabel
            //
            this.introLabel.AutoSize = true;
            this.introLabel.Location = new System.Drawing.Point(13, 23);
            this.introLabel.MaximumSize = new System.Drawing.Size(760, 0);
            this.introLabel.Name = "introLabel";
            this.introLabel.Size = new System.Drawing.Size(0, 13);
            this.introLabel.TabIndex = 1;
            //
            // searchRow
            //
            this.searchRow.AutoSize = true;
            this.searchRow.Controls.Add(this.searchLabel);
            this.searchRow.Controls.Add(this._scope);
            this.searchRow.Controls.Add(this.withinLabel);
            this.searchRow.Controls.Add(this._radius);
            this.searchRow.Controls.Add(this.ofItLabel);
            this.searchRow.Location = new System.Drawing.Point(10, 38);
            this.searchRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.searchRow.Name = "searchRow";
            this.searchRow.Size = new System.Drawing.Size(404, 27);
            this.searchRow.TabIndex = 2;
            this.searchRow.WrapContents = false;
            //
            // searchLabel
            //
            this.searchLabel.AutoSize = true;
            this.searchLabel.Location = new System.Drawing.Point(3, 0);
            this.searchLabel.Name = "searchLabel";
            this.searchLabel.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.searchLabel.Size = new System.Drawing.Size(44, 18);
            this.searchLabel.TabIndex = 0;
            this.searchLabel.Text = "Search:";
            //
            // _scope
            //
            this._scope.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._scope.Items.AddRange(new object[] {
            "The whole level",
            "Round the selected entity",
            "The composite on screen"});
            this._scope.Location = new System.Drawing.Point(53, 3);
            this._scope.Name = "_scope";
            this._scope.Size = new System.Drawing.Size(200, 21);
            this._scope.TabIndex = 1;
            this._scope.SelectedIndexChanged += new System.EventHandler(this._scope_SelectedIndexChanged);
            //
            // withinLabel
            //
            this.withinLabel.AutoSize = true;
            this.withinLabel.Location = new System.Drawing.Point(259, 0);
            this.withinLabel.Name = "withinLabel";
            this.withinLabel.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.withinLabel.Size = new System.Drawing.Size(40, 18);
            this.withinLabel.TabIndex = 2;
            this.withinLabel.Text = "  within";
            //
            // _radius
            //
            this._radius.Enabled = false;
            this._radius.Location = new System.Drawing.Point(305, 3);
            this._radius.Maximum = new decimal(new int[] {
            500,
            0,
            0,
            0});
            this._radius.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this._radius.Name = "_radius";
            this._radius.Size = new System.Drawing.Size(55, 20);
            this._radius.TabIndex = 3;
            this._radius.Value = new decimal(new int[] {
            15,
            0,
            0,
            0});
            //
            // ofItLabel
            //
            this.ofItLabel.AutoSize = true;
            this.ofItLabel.Location = new System.Drawing.Point(366, 0);
            this.ofItLabel.Name = "ofItLabel";
            this.ofItLabel.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.ofItLabel.Size = new System.Drawing.Size(35, 18);
            this.ofItLabel.TabIndex = 4;
            this.ofItLabel.Text = "m of it";
            //
            // gapRow
            //
            this.gapRow.AutoSize = true;
            this.gapRow.Controls.Add(this.closerLabel);
            this.gapRow.Controls.Add(this._gap);
            this.gapRow.Controls.Add(this.mmWithinLabel);
            this.gapRow.Controls.Add(this._angle);
            this.gapRow.Controls.Add(this.parallelLabel);
            this.gapRow.Controls.Add(this._minArea);
            this.gapRow.Controls.Add(this.areaUnitLabel);
            this.gapRow.Controls.Add(this._find);
            this.gapRow.Location = new System.Drawing.Point(10, 69);
            this.gapRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.gapRow.Name = "gapRow";
            this.gapRow.Size = new System.Drawing.Size(566, 29);
            this.gapRow.TabIndex = 3;
            this.gapRow.WrapContents = false;
            //
            // closerLabel
            //
            this.closerLabel.AutoSize = true;
            this.closerLabel.Location = new System.Drawing.Point(3, 0);
            this.closerLabel.Name = "closerLabel";
            this.closerLabel.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.closerLabel.Size = new System.Drawing.Size(60, 18);
            this.closerLabel.TabIndex = 0;
            this.closerLabel.Text = "Closer than";
            //
            // _gap
            //
            this._gap.DecimalPlaces = 2;
            this._gap.Increment = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this._gap.Location = new System.Drawing.Point(69, 3);
            this._gap.Maximum = new decimal(new int[] {
            200,
            0,
            0,
            0});
            this._gap.Minimum = new decimal(new int[] {
            5,
            0,
            0,
            131072});
            this._gap.Name = "_gap";
            this._gap.Size = new System.Drawing.Size(60, 20);
            this._gap.TabIndex = 1;
            this._gap.Value = new decimal(new int[] {
            5,
            0,
            0,
            131072});
            //
            // mmWithinLabel
            //
            this.mmWithinLabel.AutoSize = true;
            this.mmWithinLabel.Location = new System.Drawing.Point(135, 0);
            this.mmWithinLabel.Name = "mmWithinLabel";
            this.mmWithinLabel.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.mmWithinLabel.Size = new System.Drawing.Size(56, 18);
            this.mmWithinLabel.TabIndex = 2;
            this.mmWithinLabel.Text = "mm, within";
            //
            // _angle
            //
            this._angle.DecimalPlaces = 1;
            this._angle.Increment = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this._angle.Location = new System.Drawing.Point(197, 3);
            this._angle.Maximum = new decimal(new int[] {
            30,
            0,
            0,
            0});
            this._angle.Name = "_angle";
            this._angle.Size = new System.Drawing.Size(50, 20);
            this._angle.TabIndex = 3;
            //
            // parallelLabel
            //
            this.parallelLabel.AutoSize = true;
            this.parallelLabel.Location = new System.Drawing.Point(253, 0);
            this.parallelLabel.Name = "parallelLabel";
            this.parallelLabel.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.parallelLabel.Size = new System.Drawing.Size(133, 18);
            this.parallelLabel.TabIndex = 4;
            this.parallelLabel.Text = "degrees of parallel, at least";
            //
            // _minArea
            //
            this._minArea.DecimalPlaces = 1;
            this._minArea.Location = new System.Drawing.Point(392, 3);
            this._minArea.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this._minArea.Name = "_minArea";
            this._minArea.Size = new System.Drawing.Size(60, 20);
            this._minArea.TabIndex = 5;
            //
            // areaUnitLabel
            //
            this.areaUnitLabel.AutoSize = true;
            this.areaUnitLabel.Location = new System.Drawing.Point(458, 0);
            this.areaUnitLabel.Name = "areaUnitLabel";
            this.areaUnitLabel.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.areaUnitLabel.Size = new System.Drawing.Size(24, 18);
            this.areaUnitLabel.TabIndex = 6;
            this.areaUnitLabel.Text = "cm²";
            //
            // _find
            //
            this._find.AutoSize = true;
            this._find.Location = new System.Drawing.Point(488, 3);
            this._find.Name = "_find";
            this._find.Size = new System.Drawing.Size(75, 23);
            this._find.TabIndex = 7;
            this._find.Text = "Find";
            this._find.Click += new System.EventHandler(this._find_Click);
            //
            // optionsRow
            //
            this.optionsRow.AutoSize = true;
            this.optionsRow.Controls.Add(this._inside);
            this.optionsRow.Controls.Add(this._hidden);
            this.optionsRow.Controls.Add(this._decals);
            this.optionsRow.Location = new System.Drawing.Point(10, 102);
            this.optionsRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.optionsRow.Name = "optionsRow";
            this.optionsRow.Size = new System.Drawing.Size(485, 23);
            this.optionsRow.TabIndex = 4;
            this.optionsRow.WrapContents = false;
            //
            // _inside
            //
            this._inside.AutoSize = true;
            this._inside.Checked = true;
            this._inside.CheckState = System.Windows.Forms.CheckState.Checked;
            this._inside.Location = new System.Drawing.Point(3, 3);
            this._inside.Name = "_inside";
            this._inside.Size = new System.Drawing.Size(124, 17);
            this._inside.TabIndex = 0;
            this._inside.Text = "Inside one model too";
            //
            // _hidden
            //
            this._hidden.AutoSize = true;
            this._hidden.Location = new System.Drawing.Point(133, 3);
            this._hidden.Name = "_hidden";
            this._hidden.Size = new System.Drawing.Size(148, 17);
            this._hidden.TabIndex = 1;
            this._hidden.Text = "Models hidden at the start";
            //
            // _decals
            //
            this._decals.AutoSize = true;
            this._decals.Location = new System.Drawing.Point(287, 3);
            this._decals.Name = "_decals";
            this._decals.Size = new System.Drawing.Size(195, 17);
            this._decals.TabIndex = 2;
            this._decals.Text = "Mesh decals on what they decorate";
            //
            // _list
            //
            this._list.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.numberColumn,
            this.overlapColumn,
            this.apartColumn,
            this.onTopColumn,
            this.underneathColumn,
            this.kindColumn});
            this._list.FullRowSelect = true;
            this._list.HideSelection = false;
            this._list.Location = new System.Drawing.Point(13, 130);
            this._list.Name = "_list";
            this._list.Size = new System.Drawing.Size(760, 190);
            this._list.TabIndex = 5;
            this._list.View = System.Windows.Forms.View.Details;
            this._list.SelectedIndexChanged += new System.EventHandler(this._list_SelectedIndexChanged);
            //
            // numberColumn
            //
            this.numberColumn.Text = "#";
            this.numberColumn.Width = 40;
            //
            // overlapColumn
            //
            this.overlapColumn.Text = "Overlap";
            this.overlapColumn.Width = 70;
            //
            // apartColumn
            //
            this.apartColumn.Text = "Apart";
            this.apartColumn.Width = 95;
            //
            // onTopColumn
            //
            this.onTopColumn.Text = "On top";
            this.onTopColumn.Width = 240;
            //
            // underneathColumn
            //
            this.underneathColumn.Text = "Underneath";
            this.underneathColumn.Width = 210;
            //
            // kindColumn
            //
            this.kindColumn.Text = "Kind";
            this.kindColumn.Width = 95;
            //
            // _preview
            //
            this._preview.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(34)))), ((int)(((byte)(38)))));
            this._preview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this._preview.Location = new System.Drawing.Point(13, 326);
            this._preview.Name = "_preview";
            this._preview.Size = new System.Drawing.Size(760, 380);
            this._preview.TabIndex = 6;
            this._preview.TabStop = false;
            //
            // fixRow
            //
            this.fixRow.AutoSize = true;
            this.fixRow.Controls.Add(this.fixLabel);
            this.fixRow.Controls.Add(this._action);
            this.fixRow.Controls.Add(this.untilLabel);
            this.fixRow.Controls.Add(this._clearance);
            this.fixRow.Controls.Add(this.mmApartLabel);
            this.fixRow.Controls.Add(this._fixSelected);
            this.fixRow.Controls.Add(this._fixAll);
            this.fixRow.Controls.Add(this._close);
            this.fixRow.Location = new System.Drawing.Point(10, 711);
            this.fixRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.fixRow.Name = "fixRow";
            this.fixRow.Size = new System.Drawing.Size(662, 29);
            this.fixRow.TabIndex = 7;
            this.fixRow.WrapContents = false;
            //
            // fixLabel
            //
            this.fixLabel.AutoSize = true;
            this.fixLabel.Location = new System.Drawing.Point(3, 0);
            this.fixLabel.Name = "fixLabel";
            this.fixLabel.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.fixLabel.Size = new System.Drawing.Size(23, 18);
            this.fixLabel.TabIndex = 0;
            this.fixLabel.Text = "Fix:";
            //
            // _action
            //
            this._action.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._action.Items.AddRange(new object[] {
            "Push the one on top out",
            "Sink the one underneath back"});
            this._action.Location = new System.Drawing.Point(32, 3);
            this._action.Name = "_action";
            this._action.Size = new System.Drawing.Size(230, 21);
            this._action.TabIndex = 1;
            //
            // untilLabel
            //
            this.untilLabel.AutoSize = true;
            this.untilLabel.Location = new System.Drawing.Point(268, 0);
            this.untilLabel.Name = "untilLabel";
            this.untilLabel.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.untilLabel.Size = new System.Drawing.Size(26, 18);
            this.untilLabel.TabIndex = 2;
            this.untilLabel.Text = "until";
            //
            // _clearance
            //
            this._clearance.DecimalPlaces = 1;
            this._clearance.Location = new System.Drawing.Point(300, 3);
            this._clearance.Maximum = new decimal(new int[] {
            500,
            0,
            0,
            0});
            this._clearance.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this._clearance.Name = "_clearance";
            this._clearance.Size = new System.Drawing.Size(60, 20);
            this._clearance.TabIndex = 3;
            this._clearance.Value = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            //
            // mmApartLabel
            //
            this.mmApartLabel.AutoSize = true;
            this.mmApartLabel.Location = new System.Drawing.Point(366, 0);
            this.mmApartLabel.Name = "mmApartLabel";
            this.mmApartLabel.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.mmApartLabel.Size = new System.Drawing.Size(50, 18);
            this.mmApartLabel.TabIndex = 4;
            this.mmApartLabel.Text = "mm apart";
            //
            // _fixSelected
            //
            this._fixSelected.AutoSize = true;
            this._fixSelected.Enabled = false;
            this._fixSelected.Location = new System.Drawing.Point(422, 3);
            this._fixSelected.Name = "_fixSelected";
            this._fixSelected.Size = new System.Drawing.Size(75, 23);
            this._fixSelected.TabIndex = 5;
            this._fixSelected.Text = "Fix Selected";
            this._fixSelected.Click += new System.EventHandler(this._fixSelected_Click);
            //
            // _fixAll
            //
            this._fixAll.AutoSize = true;
            this._fixAll.Enabled = false;
            this._fixAll.Location = new System.Drawing.Point(503, 3);
            this._fixAll.Name = "_fixAll";
            this._fixAll.Size = new System.Drawing.Size(75, 23);
            this._fixAll.TabIndex = 6;
            this._fixAll.Text = "Fix All Listed";
            this._fixAll.Click += new System.EventHandler(this._fixAll_Click);
            //
            // _close
            //
            this._close.AutoSize = true;
            this._close.Location = new System.Drawing.Point(584, 3);
            this._close.Name = "_close";
            this._close.Size = new System.Drawing.Size(75, 23);
            this._close.TabIndex = 7;
            this._close.Text = "Close";
            this._close.Click += new System.EventHandler(this._close_Click);
            //
            // _status
            //
            this._status.AutoSize = true;
            this._status.Location = new System.Drawing.Point(13, 742);
            this._status.MaximumSize = new System.Drawing.Size(760, 0);
            this._status.Name = "_status";
            this._status.Size = new System.Drawing.Size(0, 13);
            this._status.TabIndex = 8;
            //
            // _replan
            //
            this._replan.Interval = 250;
            this._replan.Tick += new System.EventHandler(this._replan_Tick);
            //
            // ZFightWindow
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(789, 768);
            this.Controls.Add(this.rootPanel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "ZFightWindow";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.ZFightWindow_FormClosed);
            this.rootPanel.ResumeLayout(false);
            this.rootPanel.PerformLayout();
            this.searchRow.ResumeLayout(false);
            this.searchRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._radius)).EndInit();
            this.gapRow.ResumeLayout(false);
            this.gapRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._gap)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._angle)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._minArea)).EndInit();
            this.optionsRow.ResumeLayout(false);
            this.optionsRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._preview)).EndInit();
            this.fixRow.ResumeLayout(false);
            this.fixRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._clearance)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel rootPanel;
        private System.Windows.Forms.Label _title;
        private System.Windows.Forms.Label introLabel;
        private System.Windows.Forms.FlowLayoutPanel searchRow;
        private System.Windows.Forms.Label searchLabel;
        private System.Windows.Forms.ComboBox _scope;
        private System.Windows.Forms.Label withinLabel;
        private System.Windows.Forms.NumericUpDown _radius;
        private System.Windows.Forms.Label ofItLabel;
        private System.Windows.Forms.FlowLayoutPanel gapRow;
        private System.Windows.Forms.Label closerLabel;
        private System.Windows.Forms.NumericUpDown _gap;
        private System.Windows.Forms.Label mmWithinLabel;
        private System.Windows.Forms.NumericUpDown _angle;
        private System.Windows.Forms.Label parallelLabel;
        private System.Windows.Forms.NumericUpDown _minArea;
        private System.Windows.Forms.Label areaUnitLabel;
        private System.Windows.Forms.Button _find;
        private System.Windows.Forms.FlowLayoutPanel optionsRow;
        private System.Windows.Forms.CheckBox _inside;
        private System.Windows.Forms.CheckBox _hidden;
        private System.Windows.Forms.CheckBox _decals;
        private System.Windows.Forms.ListView _list;
        private System.Windows.Forms.ColumnHeader numberColumn;
        private System.Windows.Forms.ColumnHeader overlapColumn;
        private System.Windows.Forms.ColumnHeader apartColumn;
        private System.Windows.Forms.ColumnHeader onTopColumn;
        private System.Windows.Forms.ColumnHeader underneathColumn;
        private System.Windows.Forms.ColumnHeader kindColumn;
        private System.Windows.Forms.PictureBox _preview;
        private System.Windows.Forms.FlowLayoutPanel fixRow;
        private System.Windows.Forms.Label fixLabel;
        private System.Windows.Forms.ComboBox _action;
        private System.Windows.Forms.Label untilLabel;
        private System.Windows.Forms.NumericUpDown _clearance;
        private System.Windows.Forms.Label mmApartLabel;
        private System.Windows.Forms.Button _fixSelected;
        private System.Windows.Forms.Button _fixAll;
        private System.Windows.Forms.Button _close;
        private System.Windows.Forms.Label _status;
        private System.Windows.Forms.Timer _replan;
    }
}

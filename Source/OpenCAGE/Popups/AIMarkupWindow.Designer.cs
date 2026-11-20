namespace OpenCAGE.Popups
{
    partial class AIMarkupWindow
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
            this._summary = new System.Windows.Forms.TextBox();
            this._optionsPanel = new System.Windows.Forms.Panel();
            this._searchSpacingLabel = new System.Windows.Forms.Label();
            this._searchSpacing = new System.Windows.Forms.NumericUpDown();
            this._sweepSpacingLabel = new System.Windows.Forms.Label();
            this._sweepSpacing = new System.Windows.Forms.NumericUpDown();
            this._highCoverSpacingLabel = new System.Windows.Forms.Label();
            this._highCoverSpacing = new System.Windows.Forms.NumericUpDown();
            this._search = new System.Windows.Forms.CheckBox();
            this._sweep = new System.Windows.Forms.CheckBox();
            this._highCover = new System.Windows.Forms.CheckBox();
            this._lowCover = new System.Windows.Forms.CheckBox();
            this._lowCoverSpacingLabel = new System.Windows.Forms.Label();
            this._lowCoverSpacing = new System.Windows.Forms.NumericUpDown();
            this._corpseTraps = new System.Windows.Forms.CheckBox();
            this._keepClear = new System.Windows.Forms.CheckBox();
            this._indoors = new System.Windows.Forms.CheckBox();
            this._intoCaption = new System.Windows.Forms.Label();
            this._intoLabel = new System.Windows.Forms.Label();
            this._useShown = new System.Windows.Forms.Button();
            this._useRoot = new System.Windows.Forms.Button();
            this._spots = new System.Windows.Forms.ListView();
            this._colNumber = new System.Windows.Forms.ColumnHeader();
            this._colKind = new System.Windows.Forms.ColumnHeader();
            this._colPosition = new System.Windows.Forms.ColumnHeader();
            this._colClearance = new System.Windows.Forms.ColumnHeader();
            this._colFacing = new System.Windows.Forms.ColumnHeader();
            this._colWall = new System.Windows.Forms.ColumnHeader();
            this._colAndroid = new System.Windows.Forms.ColumnHeader();
            this._bottomPanel = new System.Windows.Forms.Panel();
            this._status = new System.Windows.Forms.Label();
            this._find = new System.Windows.Forms.Button();
            this._compare = new System.Windows.Forms.Button();
            this._place = new System.Windows.Forms.Button();
            this._tips = new System.Windows.Forms.ToolTip(this.components);
            this._optionsPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._searchSpacing)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._sweepSpacing)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._highCoverSpacing)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._lowCoverSpacing)).BeginInit();
            this._bottomPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // _summary
            //
            this._summary.Dock = System.Windows.Forms.DockStyle.Top;
            this._summary.Location = new System.Drawing.Point(0, 0);
            this._summary.Multiline = true;
            this._summary.Name = "_summary";
            this._summary.ReadOnly = true;
            this._summary.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._summary.Size = new System.Drawing.Size(844, 84);
            this._summary.TabIndex = 2;
            //
            // _optionsPanel
            //
            this._optionsPanel.Controls.Add(this._searchSpacingLabel);
            this._optionsPanel.Controls.Add(this._searchSpacing);
            this._optionsPanel.Controls.Add(this._sweepSpacingLabel);
            this._optionsPanel.Controls.Add(this._sweepSpacing);
            this._optionsPanel.Controls.Add(this._highCoverSpacingLabel);
            this._optionsPanel.Controls.Add(this._highCoverSpacing);
            this._optionsPanel.Controls.Add(this._search);
            this._optionsPanel.Controls.Add(this._sweep);
            this._optionsPanel.Controls.Add(this._highCover);
            this._optionsPanel.Controls.Add(this._lowCover);
            this._optionsPanel.Controls.Add(this._lowCoverSpacingLabel);
            this._optionsPanel.Controls.Add(this._lowCoverSpacing);
            this._optionsPanel.Controls.Add(this._corpseTraps);
            this._optionsPanel.Controls.Add(this._keepClear);
            this._optionsPanel.Controls.Add(this._indoors);
            this._optionsPanel.Controls.Add(this._intoCaption);
            this._optionsPanel.Controls.Add(this._intoLabel);
            this._optionsPanel.Controls.Add(this._useShown);
            this._optionsPanel.Controls.Add(this._useRoot);
            this._optionsPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this._optionsPanel.Location = new System.Drawing.Point(0, 84);
            this._optionsPanel.Name = "_optionsPanel";
            this._optionsPanel.Padding = new System.Windows.Forms.Padding(8, 6, 8, 0);
            this._optionsPanel.Size = new System.Drawing.Size(844, 198);
            this._optionsPanel.TabIndex = 1;
            //
            // _searchSpacingLabel
            //
            this._searchSpacingLabel.AutoSize = true;
            this._searchSpacingLabel.Location = new System.Drawing.Point(330, 12);
            this._searchSpacingLabel.Name = "_searchSpacingLabel";
            this._searchSpacingLabel.TabIndex = 0;
            this._searchSpacingLabel.Text = "Spacing (m):";
            //
            // _searchSpacing
            //
            this._searchSpacing.DecimalPlaces = 1;
            this._searchSpacing.Increment = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this._searchSpacing.Location = new System.Drawing.Point(420, 8);
            this._searchSpacing.Maximum = new decimal(new int[] {
            50,
            0,
            0,
            0});
            this._searchSpacing.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this._searchSpacing.Name = "_searchSpacing";
            this._searchSpacing.Size = new System.Drawing.Size(60, 20);
            this._searchSpacing.TabIndex = 1;
            this._searchSpacing.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            //
            // _sweepSpacingLabel
            //
            this._sweepSpacingLabel.AutoSize = true;
            this._sweepSpacingLabel.Location = new System.Drawing.Point(330, 40);
            this._sweepSpacingLabel.Name = "_sweepSpacingLabel";
            this._sweepSpacingLabel.TabIndex = 2;
            this._sweepSpacingLabel.Text = "Spacing (m):";
            //
            // _sweepSpacing
            //
            this._sweepSpacing.DecimalPlaces = 1;
            this._sweepSpacing.Increment = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this._sweepSpacing.Location = new System.Drawing.Point(420, 36);
            this._sweepSpacing.Maximum = new decimal(new int[] {
            50,
            0,
            0,
            0});
            this._sweepSpacing.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this._sweepSpacing.Name = "_sweepSpacing";
            this._sweepSpacing.Size = new System.Drawing.Size(60, 20);
            this._sweepSpacing.TabIndex = 3;
            this._sweepSpacing.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            //
            // _highCoverSpacingLabel
            //
            this._highCoverSpacingLabel.AutoSize = true;
            this._highCoverSpacingLabel.Location = new System.Drawing.Point(330, 68);
            this._highCoverSpacingLabel.Name = "_highCoverSpacingLabel";
            this._highCoverSpacingLabel.TabIndex = 4;
            this._highCoverSpacingLabel.Text = "Spacing (m):";
            //
            // _highCoverSpacing
            //
            this._highCoverSpacing.DecimalPlaces = 1;
            this._highCoverSpacing.Increment = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this._highCoverSpacing.Location = new System.Drawing.Point(420, 64);
            this._highCoverSpacing.Maximum = new decimal(new int[] {
            50,
            0,
            0,
            0});
            this._highCoverSpacing.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this._highCoverSpacing.Name = "_highCoverSpacing";
            this._highCoverSpacing.Size = new System.Drawing.Size(60, 20);
            this._highCoverSpacing.TabIndex = 5;
            this._highCoverSpacing.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            //
            // _search
            //
            this._search.AutoSize = true;
            this._search.Checked = true;
            this._search.CheckState = System.Windows.Forms.CheckState.Checked;
            this._search.Location = new System.Drawing.Point(8, 10);
            this._search.Name = "_search";
            this._search.TabIndex = 6;
            this._search.Text = "Search jobs (SearchJob)";
            this._tips.SetToolTip(this._search, "Spots an NPC that has lost its target searches at (the alien\'s systematic search).");
            //
            // _sweep
            //
            this._sweep.AutoSize = true;
            this._sweep.Checked = true;
            this._sweep.CheckState = System.Windows.Forms.CheckState.Checked;
            this._sweep.Location = new System.Drawing.Point(8, 38);
            this._sweep.Name = "_sweep";
            this._sweep.TabIndex = 7;
            this._sweep.Text = "Area sweep jobs (AreaSweepJob)";
            this._tips.SetToolTip(this._sweep, "Spots an NPC sweeps over while it hunts (the alien stalking).");
            //
            // _highCover
            //
            this._highCover.AutoSize = true;
            this._highCover.Location = new System.Drawing.Point(8, 66);
            this._highCover.Name = "_highCover";
            this._highCover.TabIndex = 8;
            this._highCover.Text = "Melee high cover (MeleeContext_HighCover)";
            this._tips.SetToolTip(this._highCover, "Where an android grabs the player and throws them against a wall: only levels with androids need it.");
            //
            // _lowCover
            //
            this._lowCover.AutoSize = true;
            this._lowCover.Location = new System.Drawing.Point(8, 94);
            this._lowCover.Name = "_lowCover";
            this._lowCover.TabIndex = 15;
            this._lowCover.Text = "Melee low cover (MeleeContext)";
            this._tips.SetToolTip(this._lowCover, "Where an android grabs the player and throws them onto a desk, bed or counter: only levels with androids need it.");
            //
            // _lowCoverSpacingLabel
            //
            this._lowCoverSpacingLabel.AutoSize = true;
            this._lowCoverSpacingLabel.Location = new System.Drawing.Point(330, 96);
            this._lowCoverSpacingLabel.Name = "_lowCoverSpacingLabel";
            this._lowCoverSpacingLabel.TabIndex = 16;
            this._lowCoverSpacingLabel.Text = "Spacing (m):";
            //
            // _lowCoverSpacing
            //
            this._lowCoverSpacing.DecimalPlaces = 1;
            this._lowCoverSpacing.Increment = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this._lowCoverSpacing.Location = new System.Drawing.Point(420, 92);
            this._lowCoverSpacing.Maximum = new decimal(new int[] {
            50,
            0,
            0,
            0});
            this._lowCoverSpacing.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this._lowCoverSpacing.Name = "_lowCoverSpacing";
            this._lowCoverSpacing.Size = new System.Drawing.Size(60, 20);
            this._lowCoverSpacing.TabIndex = 17;
            this._lowCoverSpacing.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            //
            // _corpseTraps
            //
            this._corpseTraps.AutoSize = true;
            this._corpseTraps.Checked = true;
            this._corpseTraps.CheckState = System.Windows.Forms.CheckState.Checked;
            this._corpseTraps.Location = new System.Drawing.Point(8, 122);
            this._corpseTraps.Name = "_corpseTraps";
            this._corpseTraps.TabIndex = 18;
            this._corpseTraps.Text = "Corpse traps round androids playing dead (MeleeContext_CorpseTrap)";
            this._tips.SetToolTip(this._corpseTraps, "Up to three round each android set to play dead (is_corpse_trap_on_start) that has none in its composite: the level needs them.");
            //
            // _keepClear
            //
            this._keepClear.AutoSize = true;
            this._keepClear.Checked = true;
            this._keepClear.CheckState = System.Windows.Forms.CheckState.Checked;
            this._keepClear.Location = new System.Drawing.Point(520, 10);
            this._keepClear.Name = "_keepClear";
            this._keepClear.TabIndex = 9;
            this._keepClear.Text = "Keep clear of the level\'s own markup";
            //
            // _indoors
            //
            this._indoors.AutoSize = true;
            this._indoors.Location = new System.Drawing.Point(520, 38);
            this._indoors.Name = "_indoors";
            this._indoors.TabIndex = 10;
            this._indoors.Text = "Indoors only (a ceiling over the spot)";
            this._tips.SetToolTip(this._indoors, "Leave out floor with no ceiling over it in the collision: for a level whose alien or androids hunt indoors.");
            //
            // _intoCaption
            //
            this._intoCaption.AutoSize = true;
            this._intoCaption.Location = new System.Drawing.Point(8, 160);
            this._intoCaption.Name = "_intoCaption";
            this._intoCaption.TabIndex = 11;
            this._intoCaption.Text = "Put them in:";
            //
            // _intoLabel
            //
            this._intoLabel.AutoEllipsis = true;
            this._intoLabel.Location = new System.Drawing.Point(110, 160);
            this._intoLabel.Name = "_intoLabel";
            this._intoLabel.Size = new System.Drawing.Size(360, 18);
            this._intoLabel.TabIndex = 12;
            //
            // _useShown
            //
            this._useShown.AutoSize = true;
            this._useShown.Location = new System.Drawing.Point(476, 154);
            this._useShown.Name = "_useShown";
            this._useShown.TabIndex = 13;
            this._useShown.Text = "Composite on Display";
            this._useShown.Click += new System.EventHandler(this._useShown_Click);
            //
            // _useRoot
            //
            this._useRoot.AutoSize = true;
            this._useRoot.Location = new System.Drawing.Point(620, 154);
            this._useRoot.Name = "_useRoot";
            this._useRoot.TabIndex = 14;
            this._useRoot.Text = "Root";
            this._useRoot.Click += new System.EventHandler(this._useRoot_Click);
            //
            // _spots
            //
            this._spots.CheckBoxes = true;
            this._spots.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colNumber,
            this._colKind,
            this._colPosition,
            this._colClearance,
            this._colFacing,
            this._colWall,
            this._colAndroid});
            this._spots.Dock = System.Windows.Forms.DockStyle.Fill;
            this._spots.FullRowSelect = true;
            this._spots.HideSelection = false;
            this._spots.Location = new System.Drawing.Point(0, 282);
            this._spots.Name = "_spots";
            this._spots.Size = new System.Drawing.Size(844, 283);
            this._spots.TabIndex = 0;
            this._spots.View = System.Windows.Forms.View.Details;
            this._spots.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this._spots_ItemChecked);
            //
            // _colNumber
            //
            this._colNumber.Text = "#";
            this._colNumber.Width = 50;
            //
            // _colKind
            //
            this._colKind.Text = "Kind";
            this._colKind.Width = 190;
            //
            // _colPosition
            //
            this._colPosition.Text = "Position (x, y, z)";
            this._colPosition.Width = 220;
            //
            // _colClearance
            //
            this._colClearance.Text = "Clearance";
            this._colClearance.Width = 80;
            //
            // _colFacing
            //
            this._colFacing.Text = "Facing";
            this._colFacing.Width = 70;
            //
            // _colWall
            //
            this._colWall.Text = "Wall / surface";
            this._colWall.Width = 90;
            //
            // _colAndroid
            //
            this._colAndroid.Text = "Android";
            this._colAndroid.Width = 220;
            //
            // _bottomPanel
            //
            this._bottomPanel.Controls.Add(this._status);
            this._bottomPanel.Controls.Add(this._find);
            this._bottomPanel.Controls.Add(this._compare);
            this._bottomPanel.Controls.Add(this._place);
            this._bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._bottomPanel.Location = new System.Drawing.Point(0, 565);
            this._bottomPanel.Name = "_bottomPanel";
            this._bottomPanel.Padding = new System.Windows.Forms.Padding(8);
            this._bottomPanel.Size = new System.Drawing.Size(844, 92);
            this._bottomPanel.TabIndex = 3;
            this._bottomPanel.Resize += new System.EventHandler(this._bottomPanel_Resize);
            //
            // _status
            //
            this._status.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this._status.Location = new System.Drawing.Point(8, 4);
            this._status.Name = "_status";
            this._status.Size = new System.Drawing.Size(828, 48);
            this._status.TabIndex = 0;
            this._status.Text = "Find Spots looks over the navmesh (as last built) for spots like retail\'s; tick the ones to keep, then Place Ticked Markup.";
            //
            // _find
            //
            this._find.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this._find.Location = new System.Drawing.Point(8, 58);
            this._find.Name = "_find";
            this._find.Size = new System.Drawing.Size(110, 28);
            this._find.TabIndex = 1;
            this._find.Text = "Find Spots";
            this._find.Click += new System.EventHandler(this._find_Click);
            //
            // _compare
            //
            this._compare.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this._compare.Location = new System.Drawing.Point(126, 58);
            this._compare.Name = "_compare";
            this._compare.Size = new System.Drawing.Size(190, 28);
            this._compare.TabIndex = 2;
            this._compare.Text = "Compare With Level\'s Own";
            this._compare.Click += new System.EventHandler(this._compare_Click);
            //
            // _place
            //
            this._place.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._place.Enabled = false;
            this._place.Location = new System.Drawing.Point(676, 58);
            this._place.Name = "_place";
            this._place.Size = new System.Drawing.Size(160, 28);
            this._place.TabIndex = 3;
            this._place.Text = "Place Ticked Markup";
            this._place.Click += new System.EventHandler(this._place_Click);
            //
            // AIMarkupWindow
            //
            this.AcceptButton = this._find;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(844, 657);
            this.Controls.Add(this._spots);
            this.Controls.Add(this._optionsPanel);
            this.Controls.Add(this._summary);
            this.Controls.Add(this._bottomPanel);
            this.MinimumSize = new System.Drawing.Size(720, 576);
            this.Name = "AIMarkupWindow";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "AI Markup";
            this._optionsPanel.ResumeLayout(false);
            this._optionsPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._searchSpacing)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._sweepSpacing)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._highCoverSpacing)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._lowCoverSpacing)).EndInit();
            this._bottomPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox _summary;
        private System.Windows.Forms.Panel _optionsPanel;
        private System.Windows.Forms.Label _searchSpacingLabel;
        private System.Windows.Forms.NumericUpDown _searchSpacing;
        private System.Windows.Forms.Label _sweepSpacingLabel;
        private System.Windows.Forms.NumericUpDown _sweepSpacing;
        private System.Windows.Forms.Label _highCoverSpacingLabel;
        private System.Windows.Forms.NumericUpDown _highCoverSpacing;
        private System.Windows.Forms.CheckBox _search;
        private System.Windows.Forms.CheckBox _sweep;
        private System.Windows.Forms.CheckBox _highCover;
        private System.Windows.Forms.CheckBox _lowCover;
        private System.Windows.Forms.Label _lowCoverSpacingLabel;
        private System.Windows.Forms.NumericUpDown _lowCoverSpacing;
        private System.Windows.Forms.CheckBox _corpseTraps;
        private System.Windows.Forms.ColumnHeader _colAndroid;
        private System.Windows.Forms.CheckBox _keepClear;
        private System.Windows.Forms.CheckBox _indoors;
        private System.Windows.Forms.Label _intoCaption;
        private System.Windows.Forms.Label _intoLabel;
        private System.Windows.Forms.Button _useShown;
        private System.Windows.Forms.Button _useRoot;
        private System.Windows.Forms.ListView _spots;
        private System.Windows.Forms.ColumnHeader _colNumber;
        private System.Windows.Forms.ColumnHeader _colKind;
        private System.Windows.Forms.ColumnHeader _colPosition;
        private System.Windows.Forms.ColumnHeader _colClearance;
        private System.Windows.Forms.ColumnHeader _colFacing;
        private System.Windows.Forms.ColumnHeader _colWall;
        private System.Windows.Forms.Panel _bottomPanel;
        private System.Windows.Forms.Label _status;
        private System.Windows.Forms.Button _find;
        private System.Windows.Forms.Button _compare;
        private System.Windows.Forms.Button _place;
        private System.Windows.Forms.ToolTip _tips;
    }
}

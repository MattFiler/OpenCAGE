namespace OpenCAGE.Popups
{
    partial class AlienVentsWindow
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
            this._countLabel = new System.Windows.Forms.Label();
            this._count = new System.Windows.Forms.NumericUpDown();
            this._countHint = new System.Windows.Forms.Label();
            this._spacingLabel = new System.Windows.Forms.Label();
            this._spacing = new System.Windows.Forms.NumericUpDown();
            this._kindLabel = new System.Windows.Forms.Label();
            this._kind = new System.Windows.Forms.ComboBox();
            this._networkLabel = new System.Windows.Forms.Label();
            this._network = new System.Windows.Forms.NumericUpDown();
            this._killtraps = new System.Windows.Forms.CheckBox();
            this._intoCaption = new System.Windows.Forms.Label();
            this._intoLabel = new System.Windows.Forms.Label();
            this._useShown = new System.Windows.Forms.Button();
            this._useRoot = new System.Windows.Forms.Button();
            this._spots = new System.Windows.Forms.ListView();
            this._colNumber = new System.Windows.Forms.ColumnHeader();
            this._colFloor = new System.Windows.Forms.ColumnHeader();
            this._colCeiling = new System.Windows.Forms.ColumnHeader();
            this._colKind = new System.Windows.Forms.ColumnHeader();
            this._colFacing = new System.Windows.Forms.ColumnHeader();
            this._colOpenAhead = new System.Windows.Forms.ColumnHeader();
            this._bottomPanel = new System.Windows.Forms.Panel();
            this._status = new System.Windows.Forms.Label();
            this._find = new System.Windows.Forms.Button();
            this._check = new System.Windows.Forms.Button();
            this._place = new System.Windows.Forms.Button();
            this._build = new System.Windows.Forms.Button();
            this._tips = new System.Windows.Forms.ToolTip(this.components);
            this._optionsPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._count)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._spacing)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._network)).BeginInit();
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
            this._optionsPanel.Controls.Add(this._countLabel);
            this._optionsPanel.Controls.Add(this._count);
            this._optionsPanel.Controls.Add(this._countHint);
            this._optionsPanel.Controls.Add(this._spacingLabel);
            this._optionsPanel.Controls.Add(this._spacing);
            this._optionsPanel.Controls.Add(this._kindLabel);
            this._optionsPanel.Controls.Add(this._kind);
            this._optionsPanel.Controls.Add(this._networkLabel);
            this._optionsPanel.Controls.Add(this._network);
            this._optionsPanel.Controls.Add(this._killtraps);
            this._optionsPanel.Controls.Add(this._intoCaption);
            this._optionsPanel.Controls.Add(this._intoLabel);
            this._optionsPanel.Controls.Add(this._useShown);
            this._optionsPanel.Controls.Add(this._useRoot);
            this._optionsPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this._optionsPanel.Location = new System.Drawing.Point(0, 84);
            this._optionsPanel.Name = "_optionsPanel";
            this._optionsPanel.Padding = new System.Windows.Forms.Padding(8, 6, 8, 0);
            this._optionsPanel.Size = new System.Drawing.Size(844, 112);
            this._optionsPanel.TabIndex = 1;
            //
            // _countLabel
            //
            this._countLabel.AutoSize = true;
            this._countLabel.Location = new System.Drawing.Point(8, 12);
            this._countLabel.Name = "_countLabel";
            this._countLabel.TabIndex = 0;
            this._countLabel.Text = "Vents to add:";
            //
            // _count
            //
            this._count.Location = new System.Drawing.Point(110, 8);
            this._count.Maximum = new decimal(new int[] {
            64,
            0,
            0,
            0});
            this._count.Name = "_count";
            this._count.Size = new System.Drawing.Size(60, 20);
            this._count.TabIndex = 1;
            //
            // _countHint
            //
            this._countHint.AutoSize = true;
            this._countHint.Location = new System.Drawing.Point(176, 12);
            this._countHint.Name = "_countHint";
            this._countHint.TabIndex = 2;
            this._countHint.Text = "(0: as the floor area calls for)";
            //
            // _spacingLabel
            //
            this._spacingLabel.AutoSize = true;
            this._spacingLabel.Location = new System.Drawing.Point(380, 12);
            this._spacingLabel.Name = "_spacingLabel";
            this._spacingLabel.TabIndex = 3;
            this._spacingLabel.Text = "Spacing (m):";
            //
            // _spacing
            //
            this._spacing.DecimalPlaces = 1;
            this._spacing.Location = new System.Drawing.Point(470, 8);
            this._spacing.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this._spacing.Name = "_spacing";
            this._spacing.Size = new System.Drawing.Size(60, 20);
            this._spacing.TabIndex = 4;
            this._spacing.Value = new decimal(new int[] {
            8,
            0,
            0,
            0});
            //
            // _kindLabel
            //
            this._kindLabel.AutoSize = true;
            this._kindLabel.Location = new System.Drawing.Point(8, 42);
            this._kindLabel.Name = "_kindLabel";
            this._kindLabel.TabIndex = 5;
            this._kindLabel.Text = "Kind:";
            //
            // _kind
            //
            this._kind.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._kind.Location = new System.Drawing.Point(110, 38);
            this._kind.Name = "_kind";
            this._kind.Size = new System.Drawing.Size(260, 21);
            this._kind.TabIndex = 6;
            //
            // _networkLabel
            //
            this._networkLabel.AutoSize = true;
            this._networkLabel.Location = new System.Drawing.Point(380, 42);
            this._networkLabel.Name = "_networkLabel";
            this._networkLabel.TabIndex = 7;
            this._networkLabel.Text = "Network ID:";
            //
            // _network
            //
            this._network.Location = new System.Drawing.Point(470, 38);
            this._network.Maximum = new decimal(new int[] {
            99,
            0,
            0,
            0});
            this._network.Name = "_network";
            this._network.Size = new System.Drawing.Size(60, 20);
            this._network.TabIndex = 8;
            //
            // _killtraps
            //
            this._killtraps.AutoSize = true;
            this._killtraps.Location = new System.Drawing.Point(546, 40);
            this._killtraps.Name = "_killtraps";
            this._killtraps.TabIndex = 9;
            this._killtraps.Text = "Kill traps on";
            this._tips.SetToolTip(this._killtraps, "Leave the vents\' kill traps on (the game\'s default): the alien in a vent can drag a player standing under it up into it. Off sets DisableKilltrapOnReset.");
            //
            // _intoCaption
            //
            this._intoCaption.AutoSize = true;
            this._intoCaption.Location = new System.Drawing.Point(8, 74);
            this._intoCaption.Name = "_intoCaption";
            this._intoCaption.TabIndex = 10;
            this._intoCaption.Text = "Put them in:";
            //
            // _intoLabel
            //
            this._intoLabel.AutoEllipsis = true;
            this._intoLabel.Location = new System.Drawing.Point(110, 74);
            this._intoLabel.Name = "_intoLabel";
            this._intoLabel.Size = new System.Drawing.Size(360, 18);
            this._intoLabel.TabIndex = 11;
            //
            // _useShown
            //
            this._useShown.AutoSize = true;
            this._useShown.Location = new System.Drawing.Point(476, 68);
            this._useShown.Name = "_useShown";
            this._useShown.TabIndex = 12;
            this._useShown.Text = "Composite on Display";
            this._useShown.Click += new System.EventHandler(this._useShown_Click);
            //
            // _useRoot
            //
            this._useRoot.AutoSize = true;
            this._useRoot.Location = new System.Drawing.Point(620, 68);
            this._useRoot.Name = "_useRoot";
            this._useRoot.TabIndex = 13;
            this._useRoot.Text = "Root";
            this._useRoot.Click += new System.EventHandler(this._useRoot_Click);
            //
            // _spots
            //
            this._spots.CheckBoxes = true;
            this._spots.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colNumber,
            this._colFloor,
            this._colCeiling,
            this._colKind,
            this._colFacing,
            this._colOpenAhead});
            this._spots.Dock = System.Windows.Forms.DockStyle.Fill;
            this._spots.FullRowSelect = true;
            this._spots.HideSelection = false;
            this._spots.Location = new System.Drawing.Point(0, 196);
            this._spots.Name = "_spots";
            this._spots.Size = new System.Drawing.Size(844, 313);
            this._spots.TabIndex = 0;
            this._spots.View = System.Windows.Forms.View.Details;
            this._spots.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this._spots_ItemChecked);
            //
            // _colNumber
            //
            this._colNumber.Text = "#";
            this._colNumber.Width = 40;
            //
            // _colFloor
            //
            this._colFloor.Text = "Floor (x, y, z)";
            this._colFloor.Width = 200;
            //
            // _colCeiling
            //
            this._colCeiling.Text = "Ceiling";
            this._colCeiling.Width = 70;
            //
            // _colKind
            //
            this._colKind.Text = "Kind";
            this._colKind.Width = 210;
            //
            // _colFacing
            //
            this._colFacing.Text = "Facing";
            this._colFacing.Width = 70;
            //
            // _colOpenAhead
            //
            this._colOpenAhead.Text = "Open ahead";
            this._colOpenAhead.Width = 90;
            //
            // _bottomPanel
            //
            this._bottomPanel.Controls.Add(this._status);
            this._bottomPanel.Controls.Add(this._find);
            this._bottomPanel.Controls.Add(this._check);
            this._bottomPanel.Controls.Add(this._place);
            this._bottomPanel.Controls.Add(this._build);
            this._bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._bottomPanel.Location = new System.Drawing.Point(0, 509);
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
            this._status.Text = "Find Spots looks over the navmesh (as last built) for ceilings a retail vent fits; tick the ones to keep, then Place Ticked Vents.";
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
            // _check
            //
            this._check.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this._check.Location = new System.Drawing.Point(126, 58);
            this._check.Name = "_check";
            this._check.Size = new System.Drawing.Size(110, 28);
            this._check.TabIndex = 2;
            this._check.Text = "Check Vents";
            this._check.Click += new System.EventHandler(this._check_Click);
            //
            // _place
            //
            this._place.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._place.Enabled = false;
            this._place.Location = new System.Drawing.Point(558, 58);
            this._place.Name = "_place";
            this._place.Size = new System.Drawing.Size(150, 28);
            this._place.TabIndex = 3;
            this._place.Text = "Place Ticked Vents";
            this._place.Click += new System.EventHandler(this._place_Click);
            //
            // _build
            //
            this._build.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._build.Location = new System.Drawing.Point(716, 58);
            this._build.Name = "_build";
            this._build.Size = new System.Drawing.Size(120, 28);
            this._build.TabIndex = 4;
            this._build.Text = "Save && Build";
            this._build.Click += new System.EventHandler(this._build_Click);
            //
            // AlienVentsWindow
            //
            this.AcceptButton = this._find;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(844, 601);
            this.Controls.Add(this._spots);
            this.Controls.Add(this._optionsPanel);
            this.Controls.Add(this._summary);
            this.Controls.Add(this._bottomPanel);
            this.MinimumSize = new System.Drawing.Size(720, 520);
            this.Name = "AlienVentsWindow";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Alien Vents";
            this._optionsPanel.ResumeLayout(false);
            this._optionsPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._count)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._spacing)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._network)).EndInit();
            this._bottomPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox _summary;
        private System.Windows.Forms.Panel _optionsPanel;
        private System.Windows.Forms.Label _countLabel;
        private System.Windows.Forms.NumericUpDown _count;
        private System.Windows.Forms.Label _countHint;
        private System.Windows.Forms.Label _spacingLabel;
        private System.Windows.Forms.NumericUpDown _spacing;
        private System.Windows.Forms.Label _kindLabel;
        private System.Windows.Forms.ComboBox _kind;
        private System.Windows.Forms.Label _networkLabel;
        private System.Windows.Forms.NumericUpDown _network;
        private System.Windows.Forms.CheckBox _killtraps;
        private System.Windows.Forms.Label _intoCaption;
        private System.Windows.Forms.Label _intoLabel;
        private System.Windows.Forms.Button _useShown;
        private System.Windows.Forms.Button _useRoot;
        private System.Windows.Forms.ListView _spots;
        private System.Windows.Forms.ColumnHeader _colNumber;
        private System.Windows.Forms.ColumnHeader _colFloor;
        private System.Windows.Forms.ColumnHeader _colCeiling;
        private System.Windows.Forms.ColumnHeader _colKind;
        private System.Windows.Forms.ColumnHeader _colFacing;
        private System.Windows.Forms.ColumnHeader _colOpenAhead;
        private System.Windows.Forms.Panel _bottomPanel;
        private System.Windows.Forms.Label _status;
        private System.Windows.Forms.Button _find;
        private System.Windows.Forms.Button _check;
        private System.Windows.Forms.Button _place;
        private System.Windows.Forms.Button _build;
        private System.Windows.Forms.ToolTip _tips;
    }
}

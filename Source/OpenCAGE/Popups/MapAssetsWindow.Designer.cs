namespace OpenCAGE.Popups
{
    partial class MapAssetsWindow
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
            this._rightPanel = new System.Windows.Forms.Panel();
            this._preview = new System.Windows.Forms.PictureBox();
            this._previewBar = new System.Windows.Forms.Panel();
            this._show = new System.Windows.Forms.Button();
            this._previewFloor = new System.Windows.Forms.ComboBox();
            this._leftPanel = new System.Windows.Forms.Panel();
            this._floors = new System.Windows.Forms.DataGridView();
            this.name = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.from = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.to = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.keyframe = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._floorButtons = new System.Windows.Forms.Panel();
            this._find = new System.Windows.Forms.Button();
            this._add = new System.Windows.Forms.Button();
            this._remove = new System.Windows.Forms.Button();
            this._optionsPanel = new System.Windows.Forms.Panel();
            this._scaleLabel = new System.Windows.Forms.Label();
            this._scale = new System.Windows.Forms.NumericUpDown();
            this._scaleHint = new System.Windows.Forms.Label();
            this._mapNameLabel = new System.Windows.Forms.Label();
            this._mapName = new System.Windows.Forms.TextBox();
            this._labelsLabel = new System.Windows.Forms.Label();
            this._labels = new System.Windows.Forms.TextBox();
            this._summary = new System.Windows.Forms.TextBox();
            this._bottomPanel = new System.Windows.Forms.Panel();
            this._status = new System.Windows.Forms.Label();
            this._generate = new System.Windows.Forms.Button();
            this._tips = new System.Windows.Forms.ToolTip(this.components);
            this._rightPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._preview)).BeginInit();
            this._previewBar.SuspendLayout();
            this._leftPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._floors)).BeginInit();
            this._floorButtons.SuspendLayout();
            this._optionsPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._scale)).BeginInit();
            this._bottomPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // _rightPanel
            //
            this._rightPanel.Controls.Add(this._preview);
            this._rightPanel.Controls.Add(this._previewBar);
            this._rightPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._rightPanel.Location = new System.Drawing.Point(430, 70);
            this._rightPanel.Name = "_rightPanel";
            this._rightPanel.Padding = new System.Windows.Forms.Padding(8);
            this._rightPanel.Size = new System.Drawing.Size(534, 535);
            this._rightPanel.TabIndex = 0;
            //
            // _preview
            //
            this._preview.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(4)))), ((int)(((byte)(8)))), ((int)(((byte)(9)))));
            this._preview.Dock = System.Windows.Forms.DockStyle.Fill;
            this._preview.Location = new System.Drawing.Point(8, 42);
            this._preview.Name = "_preview";
            this._preview.Size = new System.Drawing.Size(518, 485);
            this._preview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this._preview.TabIndex = 0;
            //
            // _previewBar
            //
            this._previewBar.Controls.Add(this._show);
            this._previewBar.Controls.Add(this._previewFloor);
            this._previewBar.Dock = System.Windows.Forms.DockStyle.Top;
            this._previewBar.Location = new System.Drawing.Point(8, 8);
            this._previewBar.Name = "_previewBar";
            this._previewBar.Size = new System.Drawing.Size(518, 34);
            this._previewBar.TabIndex = 1;
            //
            // _show
            //
            this._show.Location = new System.Drawing.Point(0, 2);
            this._show.Name = "_show";
            this._show.Size = new System.Drawing.Size(90, 28);
            this._show.TabIndex = 0;
            this._show.Text = "Preview";
            this._show.Click += new System.EventHandler(this._show_Click);
            //
            // _previewFloor
            //
            this._previewFloor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._previewFloor.Location = new System.Drawing.Point(98, 5);
            this._previewFloor.Name = "_previewFloor";
            this._previewFloor.Size = new System.Drawing.Size(240, 21);
            this._previewFloor.TabIndex = 1;
            this._previewFloor.SelectedIndexChanged += new System.EventHandler(this._previewFloor_SelectedIndexChanged);
            //
            // _leftPanel
            //
            this._leftPanel.Controls.Add(this._floors);
            this._leftPanel.Controls.Add(this._floorButtons);
            this._leftPanel.Controls.Add(this._optionsPanel);
            this._leftPanel.Dock = System.Windows.Forms.DockStyle.Left;
            this._leftPanel.Location = new System.Drawing.Point(0, 70);
            this._leftPanel.Name = "_leftPanel";
            this._leftPanel.Padding = new System.Windows.Forms.Padding(8);
            this._leftPanel.Size = new System.Drawing.Size(430, 535);
            this._leftPanel.TabIndex = 1;
            //
            // _floors
            //
            this._floors.AllowUserToAddRows = false;
            this._floors.AllowUserToDeleteRows = false;
            this._floors.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this._floors.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.name,
            this.from,
            this.to,
            this.keyframe});
            this._floors.Dock = System.Windows.Forms.DockStyle.Fill;
            this._floors.Location = new System.Drawing.Point(8, 8);
            this._floors.MultiSelect = false;
            this._floors.Name = "_floors";
            this._floors.RowHeadersVisible = false;
            this._floors.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this._floors.Size = new System.Drawing.Size(414, 327);
            this._floors.TabIndex = 0;
            this._floors.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this._floors_CellEndEdit);
            //
            // name
            //
            this.name.FillWeight = 40F;
            this.name.HeaderText = "Floor name";
            this.name.Name = "name";
            //
            // from
            //
            this.from.FillWeight = 20F;
            this.from.HeaderText = "Above (m)";
            this.from.Name = "from";
            //
            // to
            //
            this.to.FillWeight = 20F;
            this.to.HeaderText = "Up to (m)";
            this.to.Name = "to";
            //
            // keyframe
            //
            this.keyframe.FillWeight = 26F;
            this.keyframe.HeaderText = "Borrows";
            this.keyframe.Name = "keyframe";
            this.keyframe.ReadOnly = true;
            //
            // _floorButtons
            //
            this._floorButtons.Controls.Add(this._find);
            this._floorButtons.Controls.Add(this._add);
            this._floorButtons.Controls.Add(this._remove);
            this._floorButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._floorButtons.Location = new System.Drawing.Point(8, 335);
            this._floorButtons.Name = "_floorButtons";
            this._floorButtons.Size = new System.Drawing.Size(414, 36);
            this._floorButtons.TabIndex = 1;
            //
            // _find
            //
            this._find.Location = new System.Drawing.Point(0, 4);
            this._find.Name = "_find";
            this._find.Size = new System.Drawing.Size(100, 28);
            this._find.TabIndex = 0;
            this._find.Text = "Find Floors";
            this._find.Click += new System.EventHandler(this._find_Click);
            //
            // _add
            //
            this._add.Location = new System.Drawing.Point(106, 4);
            this._add.Name = "_add";
            this._add.Size = new System.Drawing.Size(90, 28);
            this._add.TabIndex = 1;
            this._add.Text = "Add Floor";
            this._add.Click += new System.EventHandler(this._add_Click);
            //
            // _remove
            //
            this._remove.Location = new System.Drawing.Point(202, 4);
            this._remove.Name = "_remove";
            this._remove.Size = new System.Drawing.Size(100, 28);
            this._remove.TabIndex = 2;
            this._remove.Text = "Remove Floor";
            this._remove.Click += new System.EventHandler(this._remove_Click);
            //
            // _optionsPanel
            //
            this._optionsPanel.Controls.Add(this._scaleLabel);
            this._optionsPanel.Controls.Add(this._scale);
            this._optionsPanel.Controls.Add(this._scaleHint);
            this._optionsPanel.Controls.Add(this._mapNameLabel);
            this._optionsPanel.Controls.Add(this._mapName);
            this._optionsPanel.Controls.Add(this._labelsLabel);
            this._optionsPanel.Controls.Add(this._labels);
            this._optionsPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._optionsPanel.Location = new System.Drawing.Point(8, 371);
            this._optionsPanel.Name = "_optionsPanel";
            this._optionsPanel.Size = new System.Drawing.Size(414, 156);
            this._optionsPanel.TabIndex = 2;
            //
            // _scaleLabel
            //
            this._scaleLabel.AutoSize = true;
            this._scaleLabel.Location = new System.Drawing.Point(0, 8);
            this._scaleLabel.Name = "_scaleLabel";
            this._scaleLabel.TabIndex = 0;
            this._scaleLabel.Text = "Map scale:";
            //
            // _scale
            //
            this._scale.DecimalPlaces = 2;
            this._scale.Increment = new decimal(new int[] {
            5,
            0,
            0,
            131072});
            this._scale.Location = new System.Drawing.Point(90, 4);
            this._scale.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this._scale.Name = "_scale";
            this._scale.Size = new System.Drawing.Size(70, 20);
            this._scale.TabIndex = 1;
            this._scale.ValueChanged += new System.EventHandler(this._option_Changed);
            //
            // _scaleHint
            //
            this._scaleHint.AutoSize = true;
            this._scaleHint.Location = new System.Drawing.Point(166, 8);
            this._scaleHint.Name = "_scaleHint";
            this._scaleHint.TabIndex = 2;
            this._scaleHint.Text = "(0: chosen to fit)";
            //
            // _mapNameLabel
            //
            this._mapNameLabel.AutoSize = true;
            this._mapNameLabel.Location = new System.Drawing.Point(0, 40);
            this._mapNameLabel.Name = "_mapNameLabel";
            this._mapNameLabel.TabIndex = 3;
            this._mapNameLabel.Text = "Map name:";
            //
            // _mapName
            //
            this._mapName.Location = new System.Drawing.Point(90, 36);
            this._mapName.Name = "_mapName";
            this._mapName.Size = new System.Drawing.Size(300, 20);
            this._mapName.TabIndex = 4;
            this._mapName.TextChanged += new System.EventHandler(this._option_Changed);
            //
            // _labelsLabel
            //
            this._labelsLabel.AutoSize = true;
            this._labelsLabel.Location = new System.Drawing.Point(0, 70);
            this._labelsLabel.Name = "_labelsLabel";
            this._labelsLabel.TabIndex = 5;
            this._labelsLabel.Text = "Labels:";
            //
            // _labels
            //
            this._labels.AcceptsReturn = true;
            this._labels.Location = new System.Drawing.Point(90, 66);
            this._labels.Multiline = true;
            this._labels.Name = "_labels";
            this._labels.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._labels.Size = new System.Drawing.Size(300, 84);
            this._labels.TabIndex = 6;
            this._tips.SetToolTip(this._labels, "Room names, one per line: name, x, z (world), and the floor\'s name when it isn\'t the lowest floor walkable there. Drawn in capitals.");
            this._labels.TextChanged += new System.EventHandler(this._option_Changed);
            //
            // _summary
            //
            this._summary.Dock = System.Windows.Forms.DockStyle.Top;
            this._summary.Location = new System.Drawing.Point(0, 0);
            this._summary.Multiline = true;
            this._summary.Name = "_summary";
            this._summary.ReadOnly = true;
            this._summary.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._summary.Size = new System.Drawing.Size(964, 70);
            this._summary.TabIndex = 2;
            this._summary.Text = "Makes the level\'s Tab map from its walk mesh (as last built): each floor is drawn in the game\'s map style, the level\'s own hub and map page and a movie per floor go in the level\'s UI folder, and a MapAnchor, floor boxes per floor and every map item\'s floor are set in the root composite (one undo step). The game only opens maps for keyframes on its own list, so the level BORROWS the game\'s (RnD_HzdLab_1, RnD_HzdLab_2, And_Lab_0...): only this level shows its map under them. Launch the level through OpenCAGE to see it.";
            //
            // _bottomPanel
            //
            this._bottomPanel.Controls.Add(this._status);
            this._bottomPanel.Controls.Add(this._generate);
            this._bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._bottomPanel.Location = new System.Drawing.Point(0, 605);
            this._bottomPanel.Name = "_bottomPanel";
            this._bottomPanel.Padding = new System.Windows.Forms.Padding(8);
            this._bottomPanel.Size = new System.Drawing.Size(964, 76);
            this._bottomPanel.TabIndex = 3;
            this._bottomPanel.Resize += new System.EventHandler(this._bottomPanel_Resize);
            //
            // _status
            //
            this._status.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this._status.Location = new System.Drawing.Point(8, 4);
            this._status.Name = "_status";
            this._status.Size = new System.Drawing.Size(948, 34);
            this._status.TabIndex = 0;
            this._status.Text = "Find Floors reads the walk mesh; rename the floors and move their heights, Preview, then Generate.";
            //
            // _generate
            //
            this._generate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._generate.Location = new System.Drawing.Point(836, 42);
            this._generate.Name = "_generate";
            this._generate.Size = new System.Drawing.Size(120, 28);
            this._generate.TabIndex = 1;
            this._generate.Text = "Generate";
            this._generate.Click += new System.EventHandler(this._generate_Click);
            //
            // MapAssetsWindow
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(964, 681);
            this.Controls.Add(this._rightPanel);
            this.Controls.Add(this._leftPanel);
            this.Controls.Add(this._summary);
            this.Controls.Add(this._bottomPanel);
            this.MinimumSize = new System.Drawing.Size(820, 560);
            this.Name = "MapAssetsWindow";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Generate Map Assets";
            this._rightPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._preview)).EndInit();
            this._previewBar.ResumeLayout(false);
            this._leftPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._floors)).EndInit();
            this._floorButtons.ResumeLayout(false);
            this._optionsPanel.ResumeLayout(false);
            this._optionsPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._scale)).EndInit();
            this._bottomPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel _rightPanel;
        private System.Windows.Forms.PictureBox _preview;
        private System.Windows.Forms.Panel _previewBar;
        private System.Windows.Forms.Button _show;
        private System.Windows.Forms.ComboBox _previewFloor;
        private System.Windows.Forms.Panel _leftPanel;
        private System.Windows.Forms.DataGridView _floors;
        private System.Windows.Forms.DataGridViewTextBoxColumn name;
        private System.Windows.Forms.DataGridViewTextBoxColumn from;
        private System.Windows.Forms.DataGridViewTextBoxColumn to;
        private System.Windows.Forms.DataGridViewTextBoxColumn keyframe;
        private System.Windows.Forms.Panel _floorButtons;
        private System.Windows.Forms.Button _find;
        private System.Windows.Forms.Button _add;
        private System.Windows.Forms.Button _remove;
        private System.Windows.Forms.Panel _optionsPanel;
        private System.Windows.Forms.Label _scaleLabel;
        private System.Windows.Forms.NumericUpDown _scale;
        private System.Windows.Forms.Label _scaleHint;
        private System.Windows.Forms.Label _mapNameLabel;
        private System.Windows.Forms.TextBox _mapName;
        private System.Windows.Forms.Label _labelsLabel;
        private System.Windows.Forms.TextBox _labels;
        private System.Windows.Forms.TextBox _summary;
        private System.Windows.Forms.Panel _bottomPanel;
        private System.Windows.Forms.Label _status;
        private System.Windows.Forms.Button _generate;
        private System.Windows.Forms.ToolTip _tips;
    }
}

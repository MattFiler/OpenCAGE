namespace OpenCAGE.Popups.Rigging
{
    partial class DisplayRigDialog
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
            this._root = new System.Windows.Forms.FlowLayoutPanel();
            this._settingsRow = new System.Windows.Forms.FlowLayoutPanel();
            this._gameplayLabel = new System.Windows.Forms.Label();
            this._gameplay = new System.Windows.Forms.ComboBox();
            this._nameLabel = new System.Windows.Forms.Label();
            this._name = new System.Windows.Forms.TextBox();
            this._scaleLabel = new System.Windows.Forms.Label();
            this._scale = new System.Windows.Forms.NumericUpDown();
            this._proportionsLabel = new System.Windows.Forms.Label();
            this._middleRow = new System.Windows.Forms.FlowLayoutPanel();
            this._bones = new System.Windows.Forms.DataGridView();
            this._boneColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this._scaleColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._offsetXColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._offsetYColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._offsetZColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._picture = new System.Windows.Forms.PictureBox();
            this._fitRow = new System.Windows.Forms.FlowLayoutPanel();
            this._fit = new System.Windows.Forms.CheckBox();
            this._source = new System.Windows.Forms.ComboBox();
            this._asLabel = new System.Windows.Forms.Label();
            this._newDisplay = new System.Windows.Forms.TextBox();
            this._archetypesLabel = new System.Windows.Forms.Label();
            this._archetypes = new System.Windows.Forms.TextBox();
            this._limits = new System.Windows.Forms.Label();
            this._status = new System.Windows.Forms.Label();
            this._buttonRow = new System.Windows.Forms.FlowLayoutPanel();
            this._create = new System.Windows.Forms.Button();
            this._cancel = new System.Windows.Forms.Button();
            this._redraw = new System.Windows.Forms.Timer(this.components);
            this._root.SuspendLayout();
            this._settingsRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._scale)).BeginInit();
            this._middleRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._bones)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._picture)).BeginInit();
            this._fitRow.SuspendLayout();
            this._buttonRow.SuspendLayout();
            this.SuspendLayout();
            //
            // _root
            //
            this._root.AutoSize = true;
            this._root.Controls.Add(this._settingsRow);
            this._root.Controls.Add(this._proportionsLabel);
            this._root.Controls.Add(this._middleRow);
            this._root.Controls.Add(this._fitRow);
            this._root.Controls.Add(this._archetypesLabel);
            this._root.Controls.Add(this._archetypes);
            this._root.Controls.Add(this._limits);
            this._root.Controls.Add(this._status);
            this._root.Controls.Add(this._buttonRow);
            this._root.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this._root.Location = new System.Drawing.Point(0, 0);
            this._root.Name = "_root";
            this._root.Padding = new System.Windows.Forms.Padding(10);
            this._root.Size = new System.Drawing.Size(1072, 563);
            this._root.TabIndex = 0;
            this._root.WrapContents = false;
            //
            // _settingsRow
            //
            this._settingsRow.AutoSize = true;
            this._settingsRow.Controls.Add(this._gameplayLabel);
            this._settingsRow.Controls.Add(this._gameplay);
            this._settingsRow.Controls.Add(this._nameLabel);
            this._settingsRow.Controls.Add(this._name);
            this._settingsRow.Controls.Add(this._scaleLabel);
            this._settingsRow.Controls.Add(this._scale);
            this._settingsRow.Location = new System.Drawing.Point(10, 12);
            this._settingsRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this._settingsRow.Name = "_settingsRow";
            this._settingsRow.Size = new System.Drawing.Size(718, 27);
            this._settingsRow.TabIndex = 0;
            this._settingsRow.WrapContents = false;
            //
            // _gameplayLabel
            //
            this._gameplayLabel.AutoSize = true;
            this._gameplayLabel.Location = new System.Drawing.Point(3, 6);
            this._gameplayLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._gameplayLabel.Name = "_gameplayLabel";
            this._gameplayLabel.Size = new System.Drawing.Size(118, 13);
            this._gameplayLabel.TabIndex = 0;
            this._gameplayLabel.Text = "Plays the animations of:";
            //
            // _gameplay
            //
            this._gameplay.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._gameplay.Location = new System.Drawing.Point(127, 3);
            this._gameplay.Name = "_gameplay";
            this._gameplay.Size = new System.Drawing.Size(160, 21);
            this._gameplay.TabIndex = 1;
            //
            // _nameLabel
            //
            this._nameLabel.AutoSize = true;
            this._nameLabel.Location = new System.Drawing.Point(293, 6);
            this._nameLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._nameLabel.Name = "_nameLabel";
            this._nameLabel.Size = new System.Drawing.Size(88, 13);
            this._nameLabel.TabIndex = 2;
            this._nameLabel.Text = "  New rig\'s name:";
            //
            // _name
            //
            this._name.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this._name.Location = new System.Drawing.Point(387, 3);
            this._name.Name = "_name";
            this._name.Size = new System.Drawing.Size(200, 20);
            this._name.TabIndex = 3;
            //
            // _scaleLabel
            //
            this._scaleLabel.AutoSize = true;
            this._scaleLabel.Location = new System.Drawing.Point(593, 6);
            this._scaleLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._scaleLabel.Name = "_scaleLabel";
            this._scaleLabel.Size = new System.Drawing.Size(36, 13);
            this._scaleLabel.TabIndex = 4;
            this._scaleLabel.Text = "  Size:";
            //
            // _scale
            //
            this._scale.DecimalPlaces = 3;
            this._scale.Increment = new decimal(new int[] {
            5,
            0,
            0,
            131072});
            this._scale.Location = new System.Drawing.Point(635, 3);
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
            this._scale.Size = new System.Drawing.Size(80, 20);
            this._scale.TabIndex = 5;
            this._scale.Value = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this._scale.ValueChanged += new System.EventHandler(this.Scale_ValueChanged);
            //
            // _proportionsLabel
            //
            this._proportionsLabel.AutoSize = true;
            this._proportionsLabel.Location = new System.Drawing.Point(13, 41);
            this._proportionsLabel.MaximumSize = new System.Drawing.Size(1040, 0);
            this._proportionsLabel.Name = "_proportionsLabel";
            this._proportionsLabel.Size = new System.Drawing.Size(583, 13);
            this._proportionsLabel.TabIndex = 1;
            this._proportionsLabel.Text = "Proportions (optional): a bone\'s scale multiplies its distance from its parent; an offset (metres, in the parent\'s frame) moves it.";
            //
            // _middleRow
            //
            this._middleRow.AutoSize = true;
            this._middleRow.Controls.Add(this._bones);
            this._middleRow.Controls.Add(this._picture);
            this._middleRow.Location = new System.Drawing.Point(10, 56);
            this._middleRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this._middleRow.Name = "_middleRow";
            this._middleRow.Size = new System.Drawing.Size(1052, 306);
            this._middleRow.TabIndex = 2;
            this._middleRow.WrapContents = false;
            //
            // _bones
            //
            this._bones.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this._bones.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this._boneColumn,
            this._scaleColumn,
            this._offsetXColumn,
            this._offsetYColumn,
            this._offsetZColumn});
            this._bones.Location = new System.Drawing.Point(3, 3);
            this._bones.Name = "_bones";
            this._bones.RowHeadersVisible = false;
            this._bones.Size = new System.Drawing.Size(520, 150);
            this._bones.TabIndex = 0;
            this._bones.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.Bones_CellValueChanged);
            this._bones.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.Bones_DataError);
            this._bones.RowsRemoved += new System.Windows.Forms.DataGridViewRowsRemovedEventHandler(this.Bones_RowsRemoved);
            //
            // _boneColumn
            //
            this._boneColumn.FillWeight = 40F;
            this._boneColumn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._boneColumn.HeaderText = "Bone";
            this._boneColumn.Name = "_boneColumn";
            //
            // _scaleColumn
            //
            this._scaleColumn.FillWeight = 15F;
            this._scaleColumn.HeaderText = "Scale";
            this._scaleColumn.Name = "_scaleColumn";
            //
            // _offsetXColumn
            //
            this._offsetXColumn.FillWeight = 15F;
            this._offsetXColumn.HeaderText = "Offset X";
            this._offsetXColumn.Name = "_offsetXColumn";
            //
            // _offsetYColumn
            //
            this._offsetYColumn.FillWeight = 15F;
            this._offsetYColumn.HeaderText = "Y";
            this._offsetYColumn.Name = "_offsetYColumn";
            //
            // _offsetZColumn
            //
            this._offsetZColumn.FillWeight = 15F;
            this._offsetZColumn.HeaderText = "Z";
            this._offsetZColumn.Name = "_offsetZColumn";
            //
            // _picture
            //
            this._picture.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this._picture.Location = new System.Drawing.Point(529, 3);
            this._picture.Name = "_picture";
            this._picture.Size = new System.Drawing.Size(520, 300);
            this._picture.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this._picture.TabIndex = 1;
            this._picture.TabStop = false;
            //
            // _fitRow
            //
            this._fitRow.AutoSize = true;
            this._fitRow.Controls.Add(this._fit);
            this._fitRow.Controls.Add(this._source);
            this._fitRow.Controls.Add(this._asLabel);
            this._fitRow.Controls.Add(this._newDisplay);
            this._fitRow.Location = new System.Drawing.Point(10, 366);
            this._fitRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this._fitRow.Name = "_fitRow";
            this._fitRow.Size = new System.Drawing.Size(767, 27);
            this._fitRow.TabIndex = 3;
            this._fitRow.WrapContents = false;
            //
            // _fit
            //
            this._fit.AutoSize = true;
            this._fit.Location = new System.Drawing.Point(3, 3);
            this._fit.Name = "_fit";
            this._fit.Size = new System.Drawing.Size(199, 17);
            this._fit.TabIndex = 0;
            this._fit.Text = "Also make a display model for it from:";
            this._fit.CheckedChanged += new System.EventHandler(this.Fit_CheckedChanged);
            //
            // _source
            //
            this._source.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._source.Enabled = false;
            this._source.Location = new System.Drawing.Point(208, 3);
            this._source.Name = "_source";
            this._source.Size = new System.Drawing.Size(260, 21);
            this._source.TabIndex = 1;
            //
            // _asLabel
            //
            this._asLabel.AutoSize = true;
            this._asLabel.Location = new System.Drawing.Point(474, 6);
            this._asLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._asLabel.Name = "_asLabel";
            this._asLabel.Size = new System.Drawing.Size(24, 13);
            this._asLabel.TabIndex = 2;
            this._asLabel.Text = "  as";
            //
            // _newDisplay
            //
            this._newDisplay.Enabled = false;
            this._newDisplay.Location = new System.Drawing.Point(504, 3);
            this._newDisplay.Name = "_newDisplay";
            this._newDisplay.Size = new System.Drawing.Size(260, 20);
            this._newDisplay.TabIndex = 3;
            //
            // _archetypesLabel
            //
            this._archetypesLabel.AutoSize = true;
            this._archetypesLabel.Location = new System.Drawing.Point(13, 395);
            this._archetypesLabel.MaximumSize = new System.Drawing.Size(1040, 0);
            this._archetypesLabel.Name = "_archetypesLabel";
            this._archetypesLabel.Size = new System.Drawing.Size(738, 13);
            this._archetypesLabel.TabIndex = 4;
            this._archetypesLabel.Text = "NPC archetypes to put on it (one composite path per line; reference_skeleton, and the display model, set on each and on every placement passing its own):";
            //
            // _archetypes
            //
            this._archetypes.Location = new System.Drawing.Point(13, 411);
            this._archetypes.Multiline = true;
            this._archetypes.Name = "_archetypes";
            this._archetypes.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._archetypes.Size = new System.Drawing.Size(1040, 54);
            this._archetypes.TabIndex = 5;
            //
            // _limits
            //
            this._limits.AutoSize = true;
            this._limits.ForeColor = System.Drawing.Color.DarkOrange;
            this._limits.Location = new System.Drawing.Point(13, 468);
            this._limits.MaximumSize = new System.Drawing.Size(1040, 0);
            this._limits.Name = "_limits";
            this._limits.Size = new System.Drawing.Size(1028, 39);
            this._limits.TabIndex = 6;
            this._limits.Text = "What a display rig can\'t change (engine): weapons, CharacterAttachmentNode attachments and the muzzle follow the GAMEPLAY rig, so a gun floats where a gameplay-size hand would be; feet slide, as the character still moves at the gameplay rig\'s speed; AI, capsule and navigation stay gameplay size; the rig keeps the gameplay rig\'s ragdoll, so a corpse is posed on gameplay-size joints. A Character uses the rig through reference_skeleton - set it on every placement that passes its own value (a placement\'s value wins over the archetype\'s).";
            //
            // _status
            //
            this._status.AutoSize = true;
            this._status.Location = new System.Drawing.Point(13, 507);
            this._status.MaximumSize = new System.Drawing.Size(1040, 0);
            this._status.Name = "_status";
            this._status.Size = new System.Drawing.Size(0, 13);
            this._status.TabIndex = 7;
            //
            // _buttonRow
            //
            this._buttonRow.AutoSize = true;
            this._buttonRow.Controls.Add(this._create);
            this._buttonRow.Controls.Add(this._cancel);
            this._buttonRow.Location = new System.Drawing.Point(10, 522);
            this._buttonRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this._buttonRow.Name = "_buttonRow";
            this._buttonRow.Size = new System.Drawing.Size(162, 29);
            this._buttonRow.TabIndex = 8;
            this._buttonRow.WrapContents = false;
            //
            // _create
            //
            this._create.AutoSize = true;
            this._create.Location = new System.Drawing.Point(3, 3);
            this._create.Name = "_create";
            this._create.Size = new System.Drawing.Size(75, 23);
            this._create.TabIndex = 0;
            this._create.Text = "Create";
            this._create.Click += new System.EventHandler(this.Create_Click);
            //
            // _cancel
            //
            this._cancel.AutoSize = true;
            this._cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._cancel.Location = new System.Drawing.Point(84, 3);
            this._cancel.Name = "_cancel";
            this._cancel.Size = new System.Drawing.Size(75, 23);
            this._cancel.TabIndex = 1;
            this._cancel.Text = "Cancel";
            //
            // _redraw
            //
            this._redraw.Interval = 250;
            this._redraw.Tick += new System.EventHandler(this.Redraw_Tick);
            //
            // DisplayRigDialog
            //
            this.AcceptButton = this._create;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.CancelButton = this._cancel;
            this.ClientSize = new System.Drawing.Size(1075, 566);
            this.Controls.Add(this._root);
            this.Name = "DisplayRigDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Create display rig";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.DisplayRigDialog_FormClosed);
            this._root.ResumeLayout(false);
            this._root.PerformLayout();
            this._settingsRow.ResumeLayout(false);
            this._settingsRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._scale)).EndInit();
            this._middleRow.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._bones)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._picture)).EndInit();
            this._fitRow.ResumeLayout(false);
            this._fitRow.PerformLayout();
            this._buttonRow.ResumeLayout(false);
            this._buttonRow.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel _root;
        private System.Windows.Forms.FlowLayoutPanel _settingsRow;
        private System.Windows.Forms.Label _gameplayLabel;
        private System.Windows.Forms.ComboBox _gameplay;
        private System.Windows.Forms.Label _nameLabel;
        private System.Windows.Forms.TextBox _name;
        private System.Windows.Forms.Label _scaleLabel;
        private System.Windows.Forms.NumericUpDown _scale;
        private System.Windows.Forms.Label _proportionsLabel;
        private System.Windows.Forms.FlowLayoutPanel _middleRow;
        private System.Windows.Forms.DataGridView _bones;
        private System.Windows.Forms.DataGridViewComboBoxColumn _boneColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn _scaleColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn _offsetXColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn _offsetYColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn _offsetZColumn;
        private System.Windows.Forms.PictureBox _picture;
        private System.Windows.Forms.FlowLayoutPanel _fitRow;
        private System.Windows.Forms.CheckBox _fit;
        private System.Windows.Forms.ComboBox _source;
        private System.Windows.Forms.Label _asLabel;
        private System.Windows.Forms.TextBox _newDisplay;
        private System.Windows.Forms.Label _archetypesLabel;
        private System.Windows.Forms.TextBox _archetypes;
        private System.Windows.Forms.Label _limits;
        private System.Windows.Forms.Label _status;
        private System.Windows.Forms.FlowLayoutPanel _buttonRow;
        private System.Windows.Forms.Button _create;
        private System.Windows.Forms.Button _cancel;
        private System.Windows.Forms.Timer _redraw;
    }
}
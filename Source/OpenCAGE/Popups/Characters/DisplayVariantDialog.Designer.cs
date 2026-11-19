namespace OpenCAGE.Popups.Characters
{
    partial class DisplayVariantDialog
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
            this._introLabel = new System.Windows.Forms.Label();
            this._nameRow = new System.Windows.Forms.FlowLayoutPanel();
            this._nameLabel = new System.Windows.Forms.Label();
            this._name = new System.Windows.Forms.TextBox();
            this._tintRow = new System.Windows.Forms.FlowLayoutPanel();
            this._tintOn = new System.Windows.Forms.CheckBox();
            this._tint = new System.Windows.Forms.Button();
            this._materialsLabel = new System.Windows.Forms.Label();
            this._materials = new System.Windows.Forms.CheckedListBox();
            this._heldOn = new System.Windows.Forms.CheckBox();
            this._fileRow = new System.Windows.Forms.FlowLayoutPanel();
            this._fileLabel = new System.Windows.Forms.Label();
            this._file = new System.Windows.Forms.TextBox();
            this._browse = new System.Windows.Forms.Button();
            this._boneRow = new System.Windows.Forms.FlowLayoutPanel();
            this._boneLabel = new System.Windows.Forms.Label();
            this._bone = new System.Windows.Forms.ComboBox();
            this._partNameLabel = new System.Windows.Forms.Label();
            this._partName = new System.Windows.Forms.TextBox();
            this._scaleLabel = new System.Windows.Forms.Label();
            this._scale = new System.Windows.Forms.NumericUpDown();
            this._offsetRow = new System.Windows.Forms.FlowLayoutPanel();
            this._offsetLabel = new System.Windows.Forms.Label();
            this._px = new System.Windows.Forms.NumericUpDown();
            this._py = new System.Windows.Forms.NumericUpDown();
            this._pz = new System.Windows.Forms.NumericUpDown();
            this._rotationLabel = new System.Windows.Forms.Label();
            this._rx = new System.Windows.Forms.NumericUpDown();
            this._ry = new System.Windows.Forms.NumericUpDown();
            this._rz = new System.Windows.Forms.NumericUpDown();
            this._report = new System.Windows.Forms.TextBox();
            this._buttonRow = new System.Windows.Forms.FlowLayoutPanel();
            this._check = new System.Windows.Forms.Button();
            this._make = new System.Windows.Forms.Button();
            this._close = new System.Windows.Forms.Button();
            this._root.SuspendLayout();
            this._nameRow.SuspendLayout();
            this._tintRow.SuspendLayout();
            this._fileRow.SuspendLayout();
            this._boneRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._scale)).BeginInit();
            this._offsetRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._px)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._py)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._pz)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._rx)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._ry)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._rz)).BeginInit();
            this._buttonRow.SuspendLayout();
            this.SuspendLayout();
            //
            // _root
            //
            this._root.AutoSize = true;
            this._root.Controls.Add(this._introLabel);
            this._root.Controls.Add(this._nameRow);
            this._root.Controls.Add(this._tintRow);
            this._root.Controls.Add(this._heldOn);
            this._root.Controls.Add(this._fileRow);
            this._root.Controls.Add(this._boneRow);
            this._root.Controls.Add(this._offsetRow);
            this._root.Controls.Add(this._report);
            this._root.Controls.Add(this._buttonRow);
            this._root.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this._root.Location = new System.Drawing.Point(0, 0);
            this._root.Name = "_root";
            this._root.Padding = new System.Windows.Forms.Padding(10);
            this._root.Size = new System.Drawing.Size(706, 525);
            this._root.TabIndex = 0;
            this._root.WrapContents = false;
            //
            // _introLabel
            //
            this._introLabel.AutoSize = true;
            this._introLabel.Location = new System.Drawing.Point(13, 10);
            this._introLabel.MaximumSize = new System.Drawing.Size(680, 0);
            this._introLabel.Name = "_introLabel";
            this._introLabel.Size = new System.Drawing.Size(265, 13);
            this._introLabel.TabIndex = 0;
            this._introLabel.Text = "What the tool makes, for the display model it opens on.";
            //
            // _nameRow
            //
            this._nameRow.AutoSize = true;
            this._nameRow.Controls.Add(this._nameLabel);
            this._nameRow.Controls.Add(this._name);
            this._nameRow.Location = new System.Drawing.Point(13, 26);
            this._nameRow.Name = "_nameRow";
            this._nameRow.Size = new System.Drawing.Size(410, 26);
            this._nameRow.TabIndex = 1;
            this._nameRow.WrapContents = false;
            //
            // _nameLabel
            //
            this._nameLabel.AutoSize = true;
            this._nameLabel.Location = new System.Drawing.Point(3, 6);
            this._nameLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._nameLabel.Name = "_nameLabel";
            this._nameLabel.Size = new System.Drawing.Size(98, 13);
            this._nameLabel.TabIndex = 0;
            this._nameLabel.Text = "New display model:";
            //
            // _name
            //
            this._name.Location = new System.Drawing.Point(107, 3);
            this._name.Name = "_name";
            this._name.Size = new System.Drawing.Size(300, 20);
            this._name.TabIndex = 1;
            //
            // _tintRow
            //
            this._tintRow.AutoSize = true;
            this._tintRow.Controls.Add(this._tintOn);
            this._tintRow.Controls.Add(this._tint);
            this._tintRow.Controls.Add(this._materialsLabel);
            this._tintRow.Controls.Add(this._materials);
            this._tintRow.Location = new System.Drawing.Point(13, 58);
            this._tintRow.Name = "_tintRow";
            this._tintRow.Size = new System.Drawing.Size(522, 70);
            this._tintRow.TabIndex = 2;
            this._tintRow.WrapContents = false;
            //
            // _tintOn
            //
            this._tintOn.AutoSize = true;
            this._tintOn.Checked = true;
            this._tintOn.CheckState = System.Windows.Forms.CheckState.Checked;
            this._tintOn.Location = new System.Drawing.Point(3, 3);
            this._tintOn.Name = "_tintOn";
            this._tintOn.Size = new System.Drawing.Size(52, 17);
            this._tintOn.TabIndex = 0;
            this._tintOn.Text = "Tint it";
            //
            // _tint
            //
            this._tint.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(64)))), ((int)(((byte)(51)))));
            this._tint.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._tint.Location = new System.Drawing.Point(61, 3);
            this._tint.Name = "_tint";
            this._tint.Size = new System.Drawing.Size(60, 23);
            this._tint.TabIndex = 1;
            this._tint.Click += new System.EventHandler(this.Tint_Click);
            //
            // _materialsLabel
            //
            this._materialsLabel.AutoSize = true;
            this._materialsLabel.Location = new System.Drawing.Point(127, 6);
            this._materialsLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._materialsLabel.Name = "_materialsLabel";
            this._materialsLabel.Size = new System.Drawing.Size(86, 13);
            this._materialsLabel.TabIndex = 2;
            this._materialsLabel.Text = "  materials to tint:";
            //
            // _materials
            //
            this._materials.CheckOnClick = true;
            this._materials.Location = new System.Drawing.Point(219, 3);
            this._materials.Name = "_materials";
            this._materials.Size = new System.Drawing.Size(300, 64);
            this._materials.TabIndex = 3;
            //
            // _heldOn
            //
            this._heldOn.AutoSize = true;
            this._heldOn.Location = new System.Drawing.Point(13, 134);
            this._heldOn.Name = "_heldOn";
            this._heldOn.Size = new System.Drawing.Size(157, 17);
            this._heldOn.TabIndex = 3;
            this._heldOn.Text = "Hold a part skinned to its rig";
            //
            // _fileRow
            //
            this._fileRow.AutoSize = true;
            this._fileRow.Controls.Add(this._fileLabel);
            this._fileRow.Controls.Add(this._file);
            this._fileRow.Controls.Add(this._browse);
            this._fileRow.Location = new System.Drawing.Point(13, 157);
            this._fileRow.Name = "_fileRow";
            this._fileRow.Size = new System.Drawing.Size(473, 29);
            this._fileRow.TabIndex = 4;
            this._fileRow.WrapContents = false;
            //
            // _fileLabel
            //
            this._fileLabel.AutoSize = true;
            this._fileLabel.Location = new System.Drawing.Point(3, 6);
            this._fileLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._fileLabel.Name = "_fileLabel";
            this._fileLabel.Size = new System.Drawing.Size(45, 13);
            this._fileLabel.TabIndex = 0;
            this._fileLabel.Text = "Part file:";
            //
            // _file
            //
            this._file.Location = new System.Drawing.Point(54, 3);
            this._file.Name = "_file";
            this._file.Size = new System.Drawing.Size(380, 20);
            this._file.TabIndex = 1;
            //
            // _browse
            //
            this._browse.Location = new System.Drawing.Point(440, 3);
            this._browse.Name = "_browse";
            this._browse.Size = new System.Drawing.Size(30, 23);
            this._browse.TabIndex = 2;
            this._browse.Text = "...";
            this._browse.Click += new System.EventHandler(this.Browse_Click);
            //
            // _boneRow
            //
            this._boneRow.AutoSize = true;
            this._boneRow.Controls.Add(this._boneLabel);
            this._boneRow.Controls.Add(this._bone);
            this._boneRow.Controls.Add(this._partNameLabel);
            this._boneRow.Controls.Add(this._partName);
            this._boneRow.Controls.Add(this._scaleLabel);
            this._boneRow.Controls.Add(this._scale);
            this._boneRow.Location = new System.Drawing.Point(13, 192);
            this._boneRow.Name = "_boneRow";
            this._boneRow.Size = new System.Drawing.Size(532, 27);
            this._boneRow.TabIndex = 5;
            this._boneRow.WrapContents = false;
            //
            // _boneLabel
            //
            this._boneLabel.AutoSize = true;
            this._boneLabel.Location = new System.Drawing.Point(3, 6);
            this._boneLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._boneLabel.Name = "_boneLabel";
            this._boneLabel.Size = new System.Drawing.Size(35, 13);
            this._boneLabel.TabIndex = 0;
            this._boneLabel.Text = "Bone:";
            //
            // _bone
            //
            this._bone.Location = new System.Drawing.Point(44, 3);
            this._bone.Name = "_bone";
            this._bone.Size = new System.Drawing.Size(170, 21);
            this._bone.TabIndex = 1;
            //
            // _partNameLabel
            //
            this._partNameLabel.AutoSize = true;
            this._partNameLabel.Location = new System.Drawing.Point(220, 6);
            this._partNameLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._partNameLabel.Name = "_partNameLabel";
            this._partNameLabel.Size = new System.Drawing.Size(64, 13);
            this._partNameLabel.TabIndex = 2;
            this._partNameLabel.Text = "  Part name:";
            //
            // _partName
            //
            this._partName.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this._partName.Location = new System.Drawing.Point(290, 3);
            this._partName.Name = "_partName";
            this._partName.Size = new System.Drawing.Size(120, 20);
            this._partName.TabIndex = 3;
            this._partName.Text = "HELD";
            //
            // _scaleLabel
            //
            this._scaleLabel.AutoSize = true;
            this._scaleLabel.Location = new System.Drawing.Point(416, 6);
            this._scaleLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._scaleLabel.Name = "_scaleLabel";
            this._scaleLabel.Size = new System.Drawing.Size(43, 13);
            this._scaleLabel.TabIndex = 4;
            this._scaleLabel.Text = "  Scale:";
            //
            // _scale
            //
            this._scale.DecimalPlaces = 3;
            this._scale.Increment = new decimal(new int[] {
            5,
            0,
            0,
            131072});
            this._scale.Location = new System.Drawing.Point(465, 3);
            this._scale.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            131072});
            this._scale.Name = "_scale";
            this._scale.Size = new System.Drawing.Size(64, 20);
            this._scale.TabIndex = 5;
            this._scale.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            //
            // _offsetRow
            //
            this._offsetRow.AutoSize = true;
            this._offsetRow.Controls.Add(this._offsetLabel);
            this._offsetRow.Controls.Add(this._px);
            this._offsetRow.Controls.Add(this._py);
            this._offsetRow.Controls.Add(this._pz);
            this._offsetRow.Controls.Add(this._rotationLabel);
            this._offsetRow.Controls.Add(this._rx);
            this._offsetRow.Controls.Add(this._ry);
            this._offsetRow.Controls.Add(this._rz);
            this._offsetRow.Location = new System.Drawing.Point(13, 225);
            this._offsetRow.Name = "_offsetRow";
            this._offsetRow.Size = new System.Drawing.Size(570, 26);
            this._offsetRow.TabIndex = 6;
            this._offsetRow.WrapContents = false;
            //
            // _offsetLabel
            //
            this._offsetLabel.AutoSize = true;
            this._offsetLabel.Location = new System.Drawing.Point(3, 6);
            this._offsetLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._offsetLabel.Name = "_offsetLabel";
            this._offsetLabel.Size = new System.Drawing.Size(55, 13);
            this._offsetLabel.TabIndex = 0;
            this._offsetLabel.Text = "Offset (m):";
            //
            // _px
            //
            this._px.DecimalPlaces = 3;
            this._px.Increment = new decimal(new int[] {
            1,
            0,
            0,
            131072});
            this._px.Location = new System.Drawing.Point(64, 3);
            this._px.Maximum = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this._px.Minimum = new decimal(new int[] {
            2,
            0,
            0,
            -2147483648});
            this._px.Name = "_px";
            this._px.Size = new System.Drawing.Size(64, 20);
            this._px.TabIndex = 1;
            //
            // _py
            //
            this._py.DecimalPlaces = 3;
            this._py.Increment = new decimal(new int[] {
            1,
            0,
            0,
            131072});
            this._py.Location = new System.Drawing.Point(134, 3);
            this._py.Maximum = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this._py.Minimum = new decimal(new int[] {
            2,
            0,
            0,
            -2147483648});
            this._py.Name = "_py";
            this._py.Size = new System.Drawing.Size(64, 20);
            this._py.TabIndex = 2;
            //
            // _pz
            //
            this._pz.DecimalPlaces = 3;
            this._pz.Increment = new decimal(new int[] {
            1,
            0,
            0,
            131072});
            this._pz.Location = new System.Drawing.Point(204, 3);
            this._pz.Maximum = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this._pz.Minimum = new decimal(new int[] {
            2,
            0,
            0,
            -2147483648});
            this._pz.Name = "_pz";
            this._pz.Size = new System.Drawing.Size(64, 20);
            this._pz.TabIndex = 3;
            //
            // _rotationLabel
            //
            this._rotationLabel.AutoSize = true;
            this._rotationLabel.Location = new System.Drawing.Point(274, 6);
            this._rotationLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._rotationLabel.Name = "_rotationLabel";
            this._rotationLabel.Size = new System.Drawing.Size(83, 13);
            this._rotationLabel.TabIndex = 4;
            this._rotationLabel.Text = "  Rotation (deg):";
            //
            // _rx
            //
            this._rx.DecimalPlaces = 1;
            this._rx.Increment = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this._rx.Location = new System.Drawing.Point(363, 3);
            this._rx.Maximum = new decimal(new int[] {
            360,
            0,
            0,
            0});
            this._rx.Minimum = new decimal(new int[] {
            360,
            0,
            0,
            -2147483648});
            this._rx.Name = "_rx";
            this._rx.Size = new System.Drawing.Size(64, 20);
            this._rx.TabIndex = 5;
            //
            // _ry
            //
            this._ry.DecimalPlaces = 1;
            this._ry.Increment = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this._ry.Location = new System.Drawing.Point(433, 3);
            this._ry.Maximum = new decimal(new int[] {
            360,
            0,
            0,
            0});
            this._ry.Minimum = new decimal(new int[] {
            360,
            0,
            0,
            -2147483648});
            this._ry.Name = "_ry";
            this._ry.Size = new System.Drawing.Size(64, 20);
            this._ry.TabIndex = 6;
            //
            // _rz
            //
            this._rz.DecimalPlaces = 1;
            this._rz.Increment = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this._rz.Location = new System.Drawing.Point(503, 3);
            this._rz.Maximum = new decimal(new int[] {
            360,
            0,
            0,
            0});
            this._rz.Minimum = new decimal(new int[] {
            360,
            0,
            0,
            -2147483648});
            this._rz.Name = "_rz";
            this._rz.Size = new System.Drawing.Size(64, 20);
            this._rz.TabIndex = 7;
            //
            // _report
            //
            this._report.Location = new System.Drawing.Point(13, 257);
            this._report.Multiline = true;
            this._report.Name = "_report";
            this._report.ReadOnly = true;
            this._report.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._report.Size = new System.Drawing.Size(680, 220);
            this._report.TabIndex = 7;
            //
            // _buttonRow
            //
            this._buttonRow.AutoSize = true;
            this._buttonRow.Controls.Add(this._check);
            this._buttonRow.Controls.Add(this._make);
            this._buttonRow.Controls.Add(this._close);
            this._buttonRow.Location = new System.Drawing.Point(13, 483);
            this._buttonRow.Name = "_buttonRow";
            this._buttonRow.Size = new System.Drawing.Size(243, 29);
            this._buttonRow.TabIndex = 8;
            this._buttonRow.WrapContents = false;
            //
            // _check
            //
            this._check.AutoSize = true;
            this._check.Location = new System.Drawing.Point(3, 3);
            this._check.Name = "_check";
            this._check.Size = new System.Drawing.Size(75, 23);
            this._check.TabIndex = 0;
            this._check.Text = "Check";
            this._check.Click += new System.EventHandler(this.Check_Click);
            //
            // _make
            //
            this._make.AutoSize = true;
            this._make.Location = new System.Drawing.Point(84, 3);
            this._make.Name = "_make";
            this._make.Size = new System.Drawing.Size(75, 23);
            this._make.TabIndex = 1;
            this._make.Text = "Make";
            this._make.Click += new System.EventHandler(this.Make_Click);
            //
            // _close
            //
            this._close.AutoSize = true;
            this._close.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._close.Location = new System.Drawing.Point(165, 3);
            this._close.Name = "_close";
            this._close.Size = new System.Drawing.Size(75, 23);
            this._close.TabIndex = 2;
            this._close.Text = "Close";
            //
            // DisplayVariantDialog
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.CancelButton = this._close;
            this.ClientSize = new System.Drawing.Size(709, 528);
            this.Controls.Add(this._root);
            this.Name = "DisplayVariantDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Make a variant";
            this._root.ResumeLayout(false);
            this._root.PerformLayout();
            this._nameRow.ResumeLayout(false);
            this._nameRow.PerformLayout();
            this._tintRow.ResumeLayout(false);
            this._tintRow.PerformLayout();
            this._fileRow.ResumeLayout(false);
            this._fileRow.PerformLayout();
            this._boneRow.ResumeLayout(false);
            this._boneRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._scale)).EndInit();
            this._offsetRow.ResumeLayout(false);
            this._offsetRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._px)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._py)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._pz)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._rx)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._ry)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._rz)).EndInit();
            this._buttonRow.ResumeLayout(false);
            this._buttonRow.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel _root;
        private System.Windows.Forms.Label _introLabel;
        private System.Windows.Forms.FlowLayoutPanel _nameRow;
        private System.Windows.Forms.Label _nameLabel;
        private System.Windows.Forms.TextBox _name;
        private System.Windows.Forms.FlowLayoutPanel _tintRow;
        private System.Windows.Forms.CheckBox _tintOn;
        private System.Windows.Forms.Button _tint;
        private System.Windows.Forms.Label _materialsLabel;
        private System.Windows.Forms.CheckedListBox _materials;
        private System.Windows.Forms.CheckBox _heldOn;
        private System.Windows.Forms.FlowLayoutPanel _fileRow;
        private System.Windows.Forms.Label _fileLabel;
        private System.Windows.Forms.TextBox _file;
        private System.Windows.Forms.Button _browse;
        private System.Windows.Forms.FlowLayoutPanel _boneRow;
        private System.Windows.Forms.Label _boneLabel;
        private System.Windows.Forms.ComboBox _bone;
        private System.Windows.Forms.Label _partNameLabel;
        private System.Windows.Forms.TextBox _partName;
        private System.Windows.Forms.Label _scaleLabel;
        private System.Windows.Forms.NumericUpDown _scale;
        private System.Windows.Forms.FlowLayoutPanel _offsetRow;
        private System.Windows.Forms.Label _offsetLabel;
        private System.Windows.Forms.NumericUpDown _px;
        private System.Windows.Forms.NumericUpDown _py;
        private System.Windows.Forms.NumericUpDown _pz;
        private System.Windows.Forms.Label _rotationLabel;
        private System.Windows.Forms.NumericUpDown _rx;
        private System.Windows.Forms.NumericUpDown _ry;
        private System.Windows.Forms.NumericUpDown _rz;
        private System.Windows.Forms.TextBox _report;
        private System.Windows.Forms.FlowLayoutPanel _buttonRow;
        private System.Windows.Forms.Button _check;
        private System.Windows.Forms.Button _make;
        private System.Windows.Forms.Button _close;
    }
}
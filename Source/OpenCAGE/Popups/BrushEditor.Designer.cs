namespace OpenCAGE
{
    partial class BrushEditor
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
            this._title = new System.Windows.Forms.Label();
            this._bounds = new System.Windows.Forms.Label();
            this._sizeRow = new System.Windows.Forms.FlowLayoutPanel();
            this._sizeXCaption = new System.Windows.Forms.Label();
            this._sizeX = new System.Windows.Forms.NumericUpDown();
            this._sizeYCaption = new System.Windows.Forms.Label();
            this._sizeY = new System.Windows.Forms.NumericUpDown();
            this._sizeZCaption = new System.Windows.Forms.Label();
            this._sizeZ = new System.Windows.Forms.NumericUpDown();
            this._collisionRow = new System.Windows.Forms.FlowLayoutPanel();
            this._collisionCaption = new System.Windows.Forms.Label();
            this._collision = new System.Windows.Forms.ComboBox();
            this._doubleSided = new System.Windows.Forms.CheckBox();
            this._facesRow = new System.Windows.Forms.FlowLayoutPanel();
            this._faces = new System.Windows.Forms.ListBox();
            this._faceTable = new System.Windows.Forms.TableLayoutPanel();
            this._materialCaption = new System.Windows.Forms.Label();
            this._material = new System.Windows.Forms.ComboBox();
            this._scaleUCaption = new System.Windows.Forms.Label();
            this._scaleU = new System.Windows.Forms.NumericUpDown();
            this._scaleVCaption = new System.Windows.Forms.Label();
            this._scaleV = new System.Windows.Forms.NumericUpDown();
            this._offsetUCaption = new System.Windows.Forms.Label();
            this._offsetU = new System.Windows.Forms.NumericUpDown();
            this._offsetVCaption = new System.Windows.Forms.Label();
            this._offsetV = new System.Windows.Forms.NumericUpDown();
            this._rotationCaption = new System.Windows.Forms.Label();
            this._rotation = new System.Windows.Forms.NumericUpDown();
            this._allFaces = new System.Windows.Forms.Button();
            this._buttonsRow = new System.Windows.Forms.FlowLayoutPanel();
            this._align = new System.Windows.Forms.Button();
            this._apply = new System.Windows.Forms.Button();
            this._close = new System.Windows.Forms.Button();
            this._status = new System.Windows.Forms.Label();
            this._root.SuspendLayout();
            this._sizeRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._sizeX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._sizeY)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._sizeZ)).BeginInit();
            this._collisionRow.SuspendLayout();
            this._facesRow.SuspendLayout();
            this._faceTable.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._scaleU)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._scaleV)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._offsetU)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._offsetV)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._rotation)).BeginInit();
            this._buttonsRow.SuspendLayout();
            this.SuspendLayout();
            //
            // _root
            //
            this._root.AutoSize = true;
            this._root.Controls.Add(this._title);
            this._root.Controls.Add(this._bounds);
            this._root.Controls.Add(this._sizeRow);
            this._root.Controls.Add(this._collisionRow);
            this._root.Controls.Add(this._facesRow);
            this._root.Controls.Add(this._buttonsRow);
            this._root.Controls.Add(this._status);
            this._root.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this._root.Location = new System.Drawing.Point(0, 0);
            this._root.Name = "_root";
            this._root.Padding = new System.Windows.Forms.Padding(8);
            this._root.TabIndex = 0;
            this._root.WrapContents = false;
            //
            // _title
            //
            this._title.AutoSize = true;
            this._title.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
            this._title.Name = "_title";
            this._title.TabIndex = 0;
            //
            // _bounds
            //
            this._bounds.AutoSize = true;
            this._bounds.Name = "_bounds";
            this._bounds.TabIndex = 1;
            //
            // _sizeRow
            //
            this._sizeRow.AutoSize = true;
            this._sizeRow.Controls.Add(this._sizeXCaption);
            this._sizeRow.Controls.Add(this._sizeX);
            this._sizeRow.Controls.Add(this._sizeYCaption);
            this._sizeRow.Controls.Add(this._sizeY);
            this._sizeRow.Controls.Add(this._sizeZCaption);
            this._sizeRow.Controls.Add(this._sizeZ);
            this._sizeRow.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this._sizeRow.Name = "_sizeRow";
            this._sizeRow.TabIndex = 2;
            this._sizeRow.WrapContents = false;
            //
            // _sizeXCaption
            //
            this._sizeXCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._sizeXCaption.AutoSize = true;
            this._sizeXCaption.Name = "_sizeXCaption";
            this._sizeXCaption.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this._sizeXCaption.TabIndex = 0;
            this._sizeXCaption.Text = "Size  X";
            //
            // _sizeX
            //
            this._sizeX.DecimalPlaces = 3;
            this._sizeX.Increment = new decimal(new int[] {
            25,
            0,
            0,
            131072});
            this._sizeX.Maximum = new decimal(new int[] {
            4096,
            0,
            0,
            0});
            this._sizeX.Minimum = new decimal(new int[] {
            10,
            0,
            0,
            196608});
            this._sizeX.Name = "_sizeX";
            this._sizeX.Size = new System.Drawing.Size(80, 20);
            this._sizeX.TabIndex = 1;
            this._sizeX.Value = new decimal(new int[] {
            10,
            0,
            0,
            196608});
            this._sizeX.ValueChanged += new System.EventHandler(this._size_ValueChanged);
            //
            // _sizeYCaption
            //
            this._sizeYCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._sizeYCaption.AutoSize = true;
            this._sizeYCaption.Name = "_sizeYCaption";
            this._sizeYCaption.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this._sizeYCaption.TabIndex = 2;
            this._sizeYCaption.Text = "Y";
            //
            // _sizeY
            //
            this._sizeY.DecimalPlaces = 3;
            this._sizeY.Increment = new decimal(new int[] {
            25,
            0,
            0,
            131072});
            this._sizeY.Maximum = new decimal(new int[] {
            4096,
            0,
            0,
            0});
            this._sizeY.Minimum = new decimal(new int[] {
            10,
            0,
            0,
            196608});
            this._sizeY.Name = "_sizeY";
            this._sizeY.Size = new System.Drawing.Size(80, 20);
            this._sizeY.TabIndex = 3;
            this._sizeY.Value = new decimal(new int[] {
            10,
            0,
            0,
            196608});
            this._sizeY.ValueChanged += new System.EventHandler(this._size_ValueChanged);
            //
            // _sizeZCaption
            //
            this._sizeZCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._sizeZCaption.AutoSize = true;
            this._sizeZCaption.Name = "_sizeZCaption";
            this._sizeZCaption.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this._sizeZCaption.TabIndex = 4;
            this._sizeZCaption.Text = "Z";
            //
            // _sizeZ
            //
            this._sizeZ.DecimalPlaces = 3;
            this._sizeZ.Increment = new decimal(new int[] {
            25,
            0,
            0,
            131072});
            this._sizeZ.Maximum = new decimal(new int[] {
            4096,
            0,
            0,
            0});
            this._sizeZ.Minimum = new decimal(new int[] {
            10,
            0,
            0,
            196608});
            this._sizeZ.Name = "_sizeZ";
            this._sizeZ.Size = new System.Drawing.Size(80, 20);
            this._sizeZ.TabIndex = 5;
            this._sizeZ.Value = new decimal(new int[] {
            10,
            0,
            0,
            196608});
            this._sizeZ.ValueChanged += new System.EventHandler(this._size_ValueChanged);
            //
            // _collisionRow
            //
            this._collisionRow.AutoSize = true;
            this._collisionRow.Controls.Add(this._collisionCaption);
            this._collisionRow.Controls.Add(this._collision);
            this._collisionRow.Controls.Add(this._doubleSided);
            this._collisionRow.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this._collisionRow.Name = "_collisionRow";
            this._collisionRow.TabIndex = 3;
            this._collisionRow.WrapContents = false;
            //
            // _collisionCaption
            //
            this._collisionCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._collisionCaption.AutoSize = true;
            this._collisionCaption.Name = "_collisionCaption";
            this._collisionCaption.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this._collisionCaption.TabIndex = 0;
            this._collisionCaption.Text = "Collision";
            //
            // _collision
            //
            this._collision.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._collision.Name = "_collision";
            this._collision.Size = new System.Drawing.Size(110, 21);
            this._collision.TabIndex = 1;
            this._collision.SelectedIndexChanged += new System.EventHandler(this._collision_SelectedIndexChanged);
            //
            // _doubleSided
            //
            this._doubleSided.AutoSize = true;
            this._doubleSided.Name = "_doubleSided";
            this._doubleSided.TabIndex = 2;
            this._doubleSided.Text = "Double sided";
            this._doubleSided.CheckedChanged += new System.EventHandler(this._doubleSided_CheckedChanged);
            //
            // _facesRow
            //
            this._facesRow.AutoSize = true;
            this._facesRow.Controls.Add(this._faces);
            this._facesRow.Controls.Add(this._faceTable);
            this._facesRow.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this._facesRow.Name = "_facesRow";
            this._facesRow.TabIndex = 4;
            this._facesRow.WrapContents = false;
            //
            // _faces
            //
            this._faces.IntegralHeight = false;
            this._faces.Name = "_faces";
            this._faces.Size = new System.Drawing.Size(150, 150);
            this._faces.TabIndex = 0;
            this._faces.SelectedIndexChanged += new System.EventHandler(this._faces_SelectedIndexChanged);
            //
            // _faceTable
            //
            this._faceTable.AutoSize = true;
            this._faceTable.ColumnCount = 4;
            this._faceTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._faceTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._faceTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._faceTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._faceTable.Controls.Add(this._materialCaption, 0, 0);
            this._faceTable.Controls.Add(this._material, 1, 0);
            this._faceTable.Controls.Add(this._scaleUCaption, 0, 1);
            this._faceTable.Controls.Add(this._scaleU, 1, 1);
            this._faceTable.Controls.Add(this._scaleVCaption, 2, 1);
            this._faceTable.Controls.Add(this._scaleV, 3, 1);
            this._faceTable.Controls.Add(this._offsetUCaption, 0, 2);
            this._faceTable.Controls.Add(this._offsetU, 1, 2);
            this._faceTable.Controls.Add(this._offsetVCaption, 2, 2);
            this._faceTable.Controls.Add(this._offsetV, 3, 2);
            this._faceTable.Controls.Add(this._rotationCaption, 0, 3);
            this._faceTable.Controls.Add(this._rotation, 1, 3);
            this._faceTable.Controls.Add(this._allFaces, 1, 4);
            this._faceTable.Name = "_faceTable";
            this._faceTable.RowCount = 5;
            this._faceTable.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._faceTable.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._faceTable.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._faceTable.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._faceTable.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._faceTable.TabIndex = 1;
            //
            // _materialCaption
            //
            this._materialCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._materialCaption.AutoSize = true;
            this._materialCaption.Name = "_materialCaption";
            this._materialCaption.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this._materialCaption.TabIndex = 0;
            this._materialCaption.Text = "Material";
            //
            // _material
            //
            this._material.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this._material.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this._faceTable.SetColumnSpan(this._material, 3);
            this._material.Name = "_material";
            this._material.Size = new System.Drawing.Size(260, 21);
            this._material.TabIndex = 1;
            this._material.TextChanged += new System.EventHandler(this._faceSetting_Changed);
            //
            // _scaleUCaption
            //
            this._scaleUCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._scaleUCaption.AutoSize = true;
            this._scaleUCaption.Name = "_scaleUCaption";
            this._scaleUCaption.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this._scaleUCaption.TabIndex = 2;
            this._scaleUCaption.Text = "Scale U";
            //
            // _scaleU
            //
            this._scaleU.DecimalPlaces = 3;
            this._scaleU.Increment = new decimal(new int[] {
            25,
            0,
            0,
            131072});
            this._scaleU.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this._scaleU.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            -2147483648});
            this._scaleU.Name = "_scaleU";
            this._scaleU.Size = new System.Drawing.Size(80, 20);
            this._scaleU.TabIndex = 3;
            this._scaleU.ValueChanged += new System.EventHandler(this._faceSetting_Changed);
            //
            // _scaleVCaption
            //
            this._scaleVCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._scaleVCaption.AutoSize = true;
            this._scaleVCaption.Name = "_scaleVCaption";
            this._scaleVCaption.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this._scaleVCaption.TabIndex = 4;
            this._scaleVCaption.Text = "V";
            //
            // _scaleV
            //
            this._scaleV.DecimalPlaces = 3;
            this._scaleV.Increment = new decimal(new int[] {
            25,
            0,
            0,
            131072});
            this._scaleV.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this._scaleV.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            -2147483648});
            this._scaleV.Name = "_scaleV";
            this._scaleV.Size = new System.Drawing.Size(80, 20);
            this._scaleV.TabIndex = 5;
            this._scaleV.ValueChanged += new System.EventHandler(this._faceSetting_Changed);
            //
            // _offsetUCaption
            //
            this._offsetUCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._offsetUCaption.AutoSize = true;
            this._offsetUCaption.Name = "_offsetUCaption";
            this._offsetUCaption.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this._offsetUCaption.TabIndex = 6;
            this._offsetUCaption.Text = "Offset U";
            //
            // _offsetU
            //
            this._offsetU.DecimalPlaces = 3;
            this._offsetU.Increment = new decimal(new int[] {
            25,
            0,
            0,
            131072});
            this._offsetU.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this._offsetU.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            -2147483648});
            this._offsetU.Name = "_offsetU";
            this._offsetU.Size = new System.Drawing.Size(80, 20);
            this._offsetU.TabIndex = 7;
            this._offsetU.ValueChanged += new System.EventHandler(this._faceSetting_Changed);
            //
            // _offsetVCaption
            //
            this._offsetVCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._offsetVCaption.AutoSize = true;
            this._offsetVCaption.Name = "_offsetVCaption";
            this._offsetVCaption.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this._offsetVCaption.TabIndex = 8;
            this._offsetVCaption.Text = "V";
            //
            // _offsetV
            //
            this._offsetV.DecimalPlaces = 3;
            this._offsetV.Increment = new decimal(new int[] {
            25,
            0,
            0,
            131072});
            this._offsetV.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this._offsetV.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            -2147483648});
            this._offsetV.Name = "_offsetV";
            this._offsetV.Size = new System.Drawing.Size(80, 20);
            this._offsetV.TabIndex = 9;
            this._offsetV.ValueChanged += new System.EventHandler(this._faceSetting_Changed);
            //
            // _rotationCaption
            //
            this._rotationCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._rotationCaption.AutoSize = true;
            this._rotationCaption.Name = "_rotationCaption";
            this._rotationCaption.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this._rotationCaption.TabIndex = 10;
            this._rotationCaption.Text = "Rotation";
            //
            // _rotation
            //
            this._rotation.DecimalPlaces = 1;
            this._rotation.Increment = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this._rotation.Maximum = new decimal(new int[] {
            360,
            0,
            0,
            0});
            this._rotation.Minimum = new decimal(new int[] {
            360,
            0,
            0,
            -2147483648});
            this._rotation.Name = "_rotation";
            this._rotation.Size = new System.Drawing.Size(80, 20);
            this._rotation.TabIndex = 11;
            this._rotation.ValueChanged += new System.EventHandler(this._faceSetting_Changed);
            //
            // _allFaces
            //
            this._allFaces.AutoSize = true;
            this._faceTable.SetColumnSpan(this._allFaces, 3);
            this._allFaces.Name = "_allFaces";
            this._allFaces.TabIndex = 12;
            this._allFaces.Text = "Texture All Faces Like This";
            this._allFaces.Click += new System.EventHandler(this._allFaces_Click);
            //
            // _buttonsRow
            //
            this._buttonsRow.AutoSize = true;
            this._buttonsRow.Controls.Add(this._align);
            this._buttonsRow.Controls.Add(this._apply);
            this._buttonsRow.Controls.Add(this._close);
            this._buttonsRow.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this._buttonsRow.Name = "_buttonsRow";
            this._buttonsRow.TabIndex = 5;
            this._buttonsRow.WrapContents = false;
            //
            // _align
            //
            this._align.AutoSize = true;
            this._align.Name = "_align";
            this._align.TabIndex = 0;
            this._align.Text = "Align Texture to World";
            this._align.Click += new System.EventHandler(this._align_Click);
            //
            // _apply
            //
            this._apply.AutoSize = true;
            this._apply.Name = "_apply";
            this._apply.TabIndex = 1;
            this._apply.Text = "Apply";
            this._apply.Click += new System.EventHandler(this._apply_Click);
            //
            // _close
            //
            this._close.AutoSize = true;
            this._close.Name = "_close";
            this._close.TabIndex = 2;
            this._close.Text = "Close";
            this._close.Click += new System.EventHandler(this._close_Click);
            //
            // _status
            //
            this._status.AutoSize = true;
            this._status.ForeColor = System.Drawing.Color.Firebrick;
            this._status.MaximumSize = new System.Drawing.Size(520, 0);
            this._status.Name = "_status";
            this._status.TabIndex = 6;
            //
            // BrushEditor
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(470, 330);
            this.Controls.Add(this._root);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.KeyPreview = true;
            this.Name = "BrushEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Brush";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.BrushEditor_KeyDown);
            this._root.ResumeLayout(false);
            this._root.PerformLayout();
            this._sizeRow.ResumeLayout(false);
            this._sizeRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._sizeX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._sizeY)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._sizeZ)).EndInit();
            this._collisionRow.ResumeLayout(false);
            this._collisionRow.PerformLayout();
            this._facesRow.ResumeLayout(false);
            this._facesRow.PerformLayout();
            this._faceTable.ResumeLayout(false);
            this._faceTable.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._scaleU)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._scaleV)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._offsetU)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._offsetV)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._rotation)).EndInit();
            this._buttonsRow.ResumeLayout(false);
            this._buttonsRow.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel _root;
        private System.Windows.Forms.Label _title;
        private System.Windows.Forms.Label _bounds;
        private System.Windows.Forms.FlowLayoutPanel _sizeRow;
        private System.Windows.Forms.Label _sizeXCaption;
        private System.Windows.Forms.NumericUpDown _sizeX;
        private System.Windows.Forms.Label _sizeYCaption;
        private System.Windows.Forms.NumericUpDown _sizeY;
        private System.Windows.Forms.Label _sizeZCaption;
        private System.Windows.Forms.NumericUpDown _sizeZ;
        private System.Windows.Forms.FlowLayoutPanel _collisionRow;
        private System.Windows.Forms.Label _collisionCaption;
        private System.Windows.Forms.ComboBox _collision;
        private System.Windows.Forms.CheckBox _doubleSided;
        private System.Windows.Forms.FlowLayoutPanel _facesRow;
        private System.Windows.Forms.ListBox _faces;
        private System.Windows.Forms.TableLayoutPanel _faceTable;
        private System.Windows.Forms.Label _materialCaption;
        private System.Windows.Forms.ComboBox _material;
        private System.Windows.Forms.Label _scaleUCaption;
        private System.Windows.Forms.NumericUpDown _scaleU;
        private System.Windows.Forms.Label _scaleVCaption;
        private System.Windows.Forms.NumericUpDown _scaleV;
        private System.Windows.Forms.Label _offsetUCaption;
        private System.Windows.Forms.NumericUpDown _offsetU;
        private System.Windows.Forms.Label _offsetVCaption;
        private System.Windows.Forms.NumericUpDown _offsetV;
        private System.Windows.Forms.Label _rotationCaption;
        private System.Windows.Forms.NumericUpDown _rotation;
        private System.Windows.Forms.Button _allFaces;
        private System.Windows.Forms.FlowLayoutPanel _buttonsRow;
        private System.Windows.Forms.Button _align;
        private System.Windows.Forms.Button _apply;
        private System.Windows.Forms.Button _close;
        private System.Windows.Forms.Label _status;
    }
}

namespace OpenCAGE.Popups
{
    partial class ReplaceWeaponModel
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
            this._root = new System.Windows.Forms.TableLayoutPanel();
            this._top = new System.Windows.Forms.FlowLayoutPanel();
            this._weaponLabel = new System.Windows.Forms.Label();
            this._weapon = new System.Windows.Forms.ComboBox();
            this._pathLabel = new System.Windows.Forms.Label();
            this._path = new System.Windows.Forms.TextBox();
            this._browse = new System.Windows.Forms.Button();
            this._reference = new System.Windows.Forms.Button();
            this._fitRow = new System.Windows.Forms.FlowLayoutPanel();
            this._oxLabel = new System.Windows.Forms.Label();
            this._ox = new System.Windows.Forms.NumericUpDown();
            this._oyLabel = new System.Windows.Forms.Label();
            this._oy = new System.Windows.Forms.NumericUpDown();
            this._ozLabel = new System.Windows.Forms.Label();
            this._oz = new System.Windows.Forms.NumericUpDown();
            this._rxLabel = new System.Windows.Forms.Label();
            this._rx = new System.Windows.Forms.NumericUpDown();
            this._ryLabel = new System.Windows.Forms.Label();
            this._ry = new System.Windows.Forms.NumericUpDown();
            this._rzLabel = new System.Windows.Forms.Label();
            this._rz = new System.Windows.Forms.NumericUpDown();
            this._scaleLabel = new System.Windows.Forms.Label();
            this._scale = new System.Windows.Forms.NumericUpDown();
            this._muzzleLabel = new System.Windows.Forms.Label();
            this._muzzle = new System.Windows.Forms.ComboBox();
            this._physicsLabel = new System.Windows.Forms.Label();
            this._physics = new System.Windows.Forms.ComboBox();
            this._prefixLabel = new System.Windows.Forms.Label();
            this._prefix = new System.Windows.Forms.TextBox();
            this._middle = new System.Windows.Forms.SplitContainer();
            this._left = new System.Windows.Forms.TableLayoutPanel();
            this._parts = new System.Windows.Forms.DataGridView();
            this._partColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._boneColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._movedColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._scriptColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._shownColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._actionColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this._meshesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._targetsLabel = new System.Windows.Forms.Label();
            this._npcCaption = new System.Windows.Forms.FlowLayoutPanel();
            this._npcLabel = new System.Windows.Forms.Label();
            this._suffix = new System.Windows.Forms.TextBox();
            this._targets = new System.Windows.Forms.CheckedListBox();
            this._npcTypes = new System.Windows.Forms.CheckedListBox();
            this._output = new System.Windows.Forms.TextBox();
            this._buttons = new System.Windows.Forms.FlowLayoutPanel();
            this._check = new System.Windows.Forms.Button();
            this._apply = new System.Windows.Forms.Button();
            this._visibility = new System.Windows.Forms.Button();
            this._close = new System.Windows.Forms.Button();
            this._status = new System.Windows.Forms.Label();
            this._root.SuspendLayout();
            this._top.SuspendLayout();
            this._fitRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._ox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._oy)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._oz)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._rx)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._ry)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._rz)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._scale)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._middle)).BeginInit();
            this._middle.Panel1.SuspendLayout();
            this._middle.SuspendLayout();
            this._left.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._parts)).BeginInit();
            this._npcCaption.SuspendLayout();
            this._buttons.SuspendLayout();
            this.SuspendLayout();
            //
            // _root
            //
            this._root.ColumnCount = 1;
            this._root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._root.Controls.Add(this._top, 0, 0);
            this._root.Controls.Add(this._fitRow, 0, 1);
            this._root.Controls.Add(this._middle, 0, 2);
            this._root.Controls.Add(this._output, 0, 3);
            this._root.Controls.Add(this._buttons, 0, 4);
            this._root.Dock = System.Windows.Forms.DockStyle.Fill;
            this._root.Location = new System.Drawing.Point(0, 0);
            this._root.Name = "_root";
            this._root.Padding = new System.Windows.Forms.Padding(6);
            this._root.RowCount = 5;
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 62F));
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 38F));
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._root.Size = new System.Drawing.Size(1264, 821);
            this._root.TabIndex = 0;
            //
            // _top
            //
            this._top.AutoSize = true;
            this._top.Controls.Add(this._weaponLabel);
            this._top.Controls.Add(this._weapon);
            this._top.Controls.Add(this._pathLabel);
            this._top.Controls.Add(this._path);
            this._top.Controls.Add(this._browse);
            this._top.Controls.Add(this._reference);
            this._top.Dock = System.Windows.Forms.DockStyle.Fill;
            this._top.Location = new System.Drawing.Point(9, 9);
            this._top.Name = "_top";
            this._top.Size = new System.Drawing.Size(1246, 29);
            this._top.TabIndex = 0;
            this._top.WrapContents = false;
            //
            // _weaponLabel
            //
            this._weaponLabel.AutoSize = true;
            this._weaponLabel.Location = new System.Drawing.Point(6, 7);
            this._weaponLabel.Margin = new System.Windows.Forms.Padding(6, 7, 2, 0);
            this._weaponLabel.Name = "_weaponLabel";
            this._weaponLabel.Size = new System.Drawing.Size(48, 13);
            this._weaponLabel.TabIndex = 0;
            this._weaponLabel.Text = "Weapon";
            //
            // _weapon
            //
            this._weapon.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._weapon.Location = new System.Drawing.Point(59, 3);
            this._weapon.Name = "_weapon";
            this._weapon.Size = new System.Drawing.Size(230, 21);
            this._weapon.TabIndex = 1;
            //
            // _pathLabel
            //
            this._pathLabel.AutoSize = true;
            this._pathLabel.Location = new System.Drawing.Point(298, 7);
            this._pathLabel.Margin = new System.Windows.Forms.Padding(6, 7, 2, 0);
            this._pathLabel.Name = "_pathLabel";
            this._pathLabel.Size = new System.Drawing.Size(126, 13);
            this._pathLabel.TabIndex = 2;
            this._pathLabel.Text = "Model (in weapon space)";
            //
            // _path
            //
            this._path.Location = new System.Drawing.Point(429, 3);
            this._path.Name = "_path";
            this._path.Size = new System.Drawing.Size(420, 20);
            this._path.TabIndex = 3;
            this._path.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Path_KeyDown);
            this._path.Leave += new System.EventHandler(this.Path_Leave);
            //
            // _browse
            //
            this._browse.AutoSize = true;
            this._browse.Location = new System.Drawing.Point(855, 3);
            this._browse.Name = "_browse";
            this._browse.Size = new System.Drawing.Size(75, 23);
            this._browse.TabIndex = 4;
            this._browse.Text = "Browse...";
            this._browse.Click += new System.EventHandler(this.Browse_Click);
            //
            // _reference
            //
            this._reference.AutoSize = true;
            this._reference.Location = new System.Drawing.Point(936, 3);
            this._reference.Name = "_reference";
            this._reference.Size = new System.Drawing.Size(109, 23);
            this._reference.TabIndex = 5;
            this._reference.Text = "Export Reference...";
            this._reference.Click += new System.EventHandler(this.Reference_Click);
            //
            // _fitRow
            //
            this._fitRow.AutoSize = true;
            this._fitRow.Controls.Add(this._oxLabel);
            this._fitRow.Controls.Add(this._ox);
            this._fitRow.Controls.Add(this._oyLabel);
            this._fitRow.Controls.Add(this._oy);
            this._fitRow.Controls.Add(this._ozLabel);
            this._fitRow.Controls.Add(this._oz);
            this._fitRow.Controls.Add(this._rxLabel);
            this._fitRow.Controls.Add(this._rx);
            this._fitRow.Controls.Add(this._ryLabel);
            this._fitRow.Controls.Add(this._ry);
            this._fitRow.Controls.Add(this._rzLabel);
            this._fitRow.Controls.Add(this._rz);
            this._fitRow.Controls.Add(this._scaleLabel);
            this._fitRow.Controls.Add(this._scale);
            this._fitRow.Controls.Add(this._muzzleLabel);
            this._fitRow.Controls.Add(this._muzzle);
            this._fitRow.Controls.Add(this._physicsLabel);
            this._fitRow.Controls.Add(this._physics);
            this._fitRow.Controls.Add(this._prefixLabel);
            this._fitRow.Controls.Add(this._prefix);
            this._fitRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this._fitRow.Location = new System.Drawing.Point(9, 44);
            this._fitRow.Name = "_fitRow";
            this._fitRow.Size = new System.Drawing.Size(1246, 53);
            this._fitRow.TabIndex = 1;
            //
            // _oxLabel
            //
            this._oxLabel.AutoSize = true;
            this._oxLabel.Location = new System.Drawing.Point(6, 7);
            this._oxLabel.Margin = new System.Windows.Forms.Padding(6, 7, 2, 0);
            this._oxLabel.Name = "_oxLabel";
            this._oxLabel.Size = new System.Drawing.Size(65, 13);
            this._oxLabel.TabIndex = 0;
            this._oxLabel.Text = "Offset (m)  X";
            //
            // _ox
            //
            this._ox.DecimalPlaces = 4;
            this._ox.Increment = new decimal(new int[] {
            5,
            0,
            0,
            196608});
            this._ox.Location = new System.Drawing.Point(76, 3);
            this._ox.Maximum = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this._ox.Minimum = new decimal(new int[] {
            2,
            0,
            0,
            -2147483648});
            this._ox.Name = "_ox";
            this._ox.Size = new System.Drawing.Size(72, 20);
            this._ox.TabIndex = 1;
            this._ox.ValueChanged += new System.EventHandler(this.Placement_ValueChanged);
            //
            // _oyLabel
            //
            this._oyLabel.AutoSize = true;
            this._oyLabel.Location = new System.Drawing.Point(157, 7);
            this._oyLabel.Margin = new System.Windows.Forms.Padding(6, 7, 2, 0);
            this._oyLabel.Name = "_oyLabel";
            this._oyLabel.Size = new System.Drawing.Size(14, 13);
            this._oyLabel.TabIndex = 2;
            this._oyLabel.Text = "Y";
            //
            // _oy
            //
            this._oy.DecimalPlaces = 4;
            this._oy.Increment = new decimal(new int[] {
            5,
            0,
            0,
            196608});
            this._oy.Location = new System.Drawing.Point(176, 3);
            this._oy.Maximum = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this._oy.Minimum = new decimal(new int[] {
            2,
            0,
            0,
            -2147483648});
            this._oy.Name = "_oy";
            this._oy.Size = new System.Drawing.Size(72, 20);
            this._oy.TabIndex = 3;
            this._oy.ValueChanged += new System.EventHandler(this.Placement_ValueChanged);
            //
            // _ozLabel
            //
            this._ozLabel.AutoSize = true;
            this._ozLabel.Location = new System.Drawing.Point(257, 7);
            this._ozLabel.Margin = new System.Windows.Forms.Padding(6, 7, 2, 0);
            this._ozLabel.Name = "_ozLabel";
            this._ozLabel.Size = new System.Drawing.Size(14, 13);
            this._ozLabel.TabIndex = 4;
            this._ozLabel.Text = "Z";
            //
            // _oz
            //
            this._oz.DecimalPlaces = 4;
            this._oz.Increment = new decimal(new int[] {
            5,
            0,
            0,
            196608});
            this._oz.Location = new System.Drawing.Point(276, 3);
            this._oz.Maximum = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this._oz.Minimum = new decimal(new int[] {
            2,
            0,
            0,
            -2147483648});
            this._oz.Name = "_oz";
            this._oz.Size = new System.Drawing.Size(72, 20);
            this._oz.TabIndex = 5;
            this._oz.ValueChanged += new System.EventHandler(this.Placement_ValueChanged);
            //
            // _rxLabel
            //
            this._rxLabel.AutoSize = true;
            this._rxLabel.Location = new System.Drawing.Point(357, 7);
            this._rxLabel.Margin = new System.Windows.Forms.Padding(6, 7, 2, 0);
            this._rxLabel.Name = "_rxLabel";
            this._rxLabel.Size = new System.Drawing.Size(60, 13);
            this._rxLabel.TabIndex = 6;
            this._rxLabel.Text = "Rotation  X";
            //
            // _rx
            //
            this._rx.DecimalPlaces = 2;
            this._rx.Location = new System.Drawing.Point(422, 3);
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
            this._rx.Size = new System.Drawing.Size(72, 20);
            this._rx.TabIndex = 7;
            this._rx.ValueChanged += new System.EventHandler(this.Placement_ValueChanged);
            //
            // _ryLabel
            //
            this._ryLabel.AutoSize = true;
            this._ryLabel.Location = new System.Drawing.Point(503, 7);
            this._ryLabel.Margin = new System.Windows.Forms.Padding(6, 7, 2, 0);
            this._ryLabel.Name = "_ryLabel";
            this._ryLabel.Size = new System.Drawing.Size(14, 13);
            this._ryLabel.TabIndex = 8;
            this._ryLabel.Text = "Y";
            //
            // _ry
            //
            this._ry.DecimalPlaces = 2;
            this._ry.Location = new System.Drawing.Point(522, 3);
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
            this._ry.Size = new System.Drawing.Size(72, 20);
            this._ry.TabIndex = 9;
            this._ry.ValueChanged += new System.EventHandler(this.Placement_ValueChanged);
            //
            // _rzLabel
            //
            this._rzLabel.AutoSize = true;
            this._rzLabel.Location = new System.Drawing.Point(603, 7);
            this._rzLabel.Margin = new System.Windows.Forms.Padding(6, 7, 2, 0);
            this._rzLabel.Name = "_rzLabel";
            this._rzLabel.Size = new System.Drawing.Size(14, 13);
            this._rzLabel.TabIndex = 10;
            this._rzLabel.Text = "Z";
            //
            // _rz
            //
            this._rz.DecimalPlaces = 2;
            this._rz.Location = new System.Drawing.Point(622, 3);
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
            this._rz.Size = new System.Drawing.Size(72, 20);
            this._rz.TabIndex = 11;
            this._rz.ValueChanged += new System.EventHandler(this.Placement_ValueChanged);
            //
            // _scaleLabel
            //
            this._scaleLabel.AutoSize = true;
            this._scaleLabel.Location = new System.Drawing.Point(703, 7);
            this._scaleLabel.Margin = new System.Windows.Forms.Padding(6, 7, 2, 0);
            this._scaleLabel.Name = "_scaleLabel";
            this._scaleLabel.Size = new System.Drawing.Size(34, 13);
            this._scaleLabel.TabIndex = 12;
            this._scaleLabel.Text = "Scale";
            //
            // _scale
            //
            this._scale.DecimalPlaces = 4;
            this._scale.Increment = new decimal(new int[] {
            1,
            0,
            0,
            131072});
            this._scale.Location = new System.Drawing.Point(742, 3);
            this._scale.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this._scale.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            262144});
            this._scale.Name = "_scale";
            this._scale.Size = new System.Drawing.Size(72, 20);
            this._scale.TabIndex = 13;
            this._scale.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this._scale.ValueChanged += new System.EventHandler(this.Scale_ValueChanged);
            //
            // _muzzleLabel
            //
            this._muzzleLabel.AutoSize = true;
            this._muzzleLabel.Location = new System.Drawing.Point(823, 7);
            this._muzzleLabel.Margin = new System.Windows.Forms.Padding(6, 7, 2, 0);
            this._muzzleLabel.Name = "_muzzleLabel";
            this._muzzleLabel.Size = new System.Drawing.Size(40, 13);
            this._muzzleLabel.TabIndex = 14;
            this._muzzleLabel.Text = "Muzzle";
            //
            // _muzzle
            //
            this._muzzle.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._muzzle.Items.AddRange(new object[] {
            "auto",
            "keep"});
            this._muzzle.Location = new System.Drawing.Point(868, 3);
            this._muzzle.Name = "_muzzle";
            this._muzzle.Size = new System.Drawing.Size(90, 21);
            this._muzzle.TabIndex = 15;
            //
            // _physicsLabel
            //
            this._physicsLabel.AutoSize = true;
            this._physicsLabel.Location = new System.Drawing.Point(967, 7);
            this._physicsLabel.Margin = new System.Windows.Forms.Padding(6, 7, 2, 0);
            this._physicsLabel.Name = "_physicsLabel";
            this._physicsLabel.Size = new System.Drawing.Size(43, 13);
            this._physicsLabel.TabIndex = 16;
            this._physicsLabel.Text = "Physics";
            //
            // _physics
            //
            this._physics.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._physics.Items.AddRange(new object[] {
            "auto",
            "hull",
            "keep"});
            this._physics.Location = new System.Drawing.Point(1015, 3);
            this._physics.Name = "_physics";
            this._physics.Size = new System.Drawing.Size(90, 21);
            this._physics.TabIndex = 17;
            //
            // _prefixLabel
            //
            this._prefixLabel.AutoSize = true;
            this._prefixLabel.Location = new System.Drawing.Point(1114, 7);
            this._prefixLabel.Margin = new System.Windows.Forms.Padding(6, 7, 2, 0);
            this._prefixLabel.Name = "_prefixLabel";
            this._prefixLabel.Size = new System.Drawing.Size(33, 13);
            this._prefixLabel.TabIndex = 18;
            this._prefixLabel.Text = "Prefix";
            //
            // _prefix
            //
            this._prefix.Location = new System.Drawing.Point(3, 30);
            this._prefix.Name = "_prefix";
            this._prefix.Size = new System.Drawing.Size(200, 20);
            this._prefix.TabIndex = 19;
            //
            // _middle
            //
            this._middle.Dock = System.Windows.Forms.DockStyle.Fill;
            this._middle.Location = new System.Drawing.Point(9, 103);
            this._middle.Name = "_middle";
            //
            // _middle.Panel1
            //
            this._middle.Panel1.Controls.Add(this._left);
            this._middle.Size = new System.Drawing.Size(1246, 415);
            this._middle.SplitterDistance = 685;
            this._middle.TabIndex = 2;
            //
            // _left
            //
            this._left.ColumnCount = 2;
            this._left.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this._left.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this._left.Controls.Add(this._parts, 0, 0);
            this._left.Controls.Add(this._targetsLabel, 0, 1);
            this._left.Controls.Add(this._npcCaption, 1, 1);
            this._left.Controls.Add(this._targets, 0, 2);
            this._left.Controls.Add(this._npcTypes, 1, 2);
            this._left.Dock = System.Windows.Forms.DockStyle.Fill;
            this._left.Location = new System.Drawing.Point(0, 0);
            this._left.Name = "_left";
            this._left.RowCount = 3;
            this._left.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 68F));
            this._left.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._left.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 32F));
            this._left.Size = new System.Drawing.Size(685, 415);
            this._left.TabIndex = 0;
            this._left.SetColumnSpan(this._parts, 2);
            //
            // _parts
            //
            this._parts.AllowUserToAddRows = false;
            this._parts.AllowUserToDeleteRows = false;
            this._parts.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this._parts.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this._partColumn,
            this._boneColumn,
            this._movedColumn,
            this._scriptColumn,
            this._shownColumn,
            this._actionColumn,
            this._meshesColumn});
            this._parts.Dock = System.Windows.Forms.DockStyle.Fill;
            this._parts.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this._parts.Location = new System.Drawing.Point(3, 3);
            this._parts.Name = "_parts";
            this._parts.RowHeadersVisible = false;
            this._parts.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this._parts.Size = new System.Drawing.Size(679, 254);
            this._parts.TabIndex = 0;
            this._parts.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.Parts_CellValueChanged);
            this._parts.CurrentCellDirtyStateChanged += new System.EventHandler(this.Parts_CurrentCellDirtyStateChanged);
            this._parts.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.Parts_DataError);
            this._parts.SelectionChanged += new System.EventHandler(this.Parts_SelectionChanged);
            //
            // _partColumn
            //
            this._partColumn.FillWeight = 18F;
            this._partColumn.HeaderText = "Part";
            this._partColumn.Name = "_partColumn";
            this._partColumn.ReadOnly = true;
            //
            // _boneColumn
            //
            this._boneColumn.FillWeight = 14F;
            this._boneColumn.HeaderText = "Bone";
            this._boneColumn.Name = "_boneColumn";
            this._boneColumn.ReadOnly = true;
            //
            // _movedColumn
            //
            this._movedColumn.FillWeight = 26F;
            this._movedColumn.HeaderText = "Moved by (clips)";
            this._movedColumn.Name = "_movedColumn";
            this._movedColumn.ReadOnly = true;
            //
            // _scriptColumn
            //
            this._scriptColumn.FillWeight = 12F;
            this._scriptColumn.HeaderText = "Script";
            this._scriptColumn.Name = "_scriptColumn";
            this._scriptColumn.ReadOnly = true;
            //
            // _shownColumn
            //
            this._shownColumn.FillWeight = 10F;
            this._shownColumn.HeaderText = "Shown now";
            this._shownColumn.Name = "_shownColumn";
            this._shownColumn.ReadOnly = true;
            //
            // _actionColumn
            //
            this._actionColumn.FillWeight = 12F;
            this._actionColumn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._actionColumn.HeaderText = "Action";
            this._actionColumn.Items.AddRange(new object[] {
            "auto",
            "replace",
            "keep",
            "hide",
            "show"});
            this._actionColumn.Name = "_actionColumn";
            //
            // _meshesColumn
            //
            this._meshesColumn.FillWeight = 18F;
            this._meshesColumn.HeaderText = "Meshes (replace)";
            this._meshesColumn.Name = "_meshesColumn";
            //
            // _targetsLabel
            //
            this._targetsLabel.AutoSize = true;
            this._targetsLabel.Location = new System.Drawing.Point(3, 260);
            this._targetsLabel.Name = "_targetsLabel";
            this._targetsLabel.Size = new System.Drawing.Size(129, 13);
            this._targetsLabel.TabIndex = 1;
            this._targetsLabel.Text = "Change these composites";
            //
            // _npcCaption
            //
            this._npcCaption.AutoSize = true;
            this._npcCaption.Controls.Add(this._npcLabel);
            this._npcCaption.Controls.Add(this._suffix);
            this._npcCaption.Location = new System.Drawing.Point(414, 263);
            this._npcCaption.Name = "_npcCaption";
            this._npcCaption.Size = new System.Drawing.Size(268, 26);
            this._npcCaption.TabIndex = 2;
            this._npcCaption.WrapContents = false;
            //
            // _npcLabel
            //
            this._npcLabel.AutoSize = true;
            this._npcLabel.Location = new System.Drawing.Point(0, 4);
            this._npcLabel.Margin = new System.Windows.Forms.Padding(0, 4, 2, 0);
            this._npcLabel.Name = "_npcLabel";
            this._npcLabel.Size = new System.Drawing.Size(271, 13);
            this._npcLabel.TabIndex = 0;
            this._npcLabel.Text = "Third person only for (none = every NPC); copies named";
            //
            // _suffix
            //
            this._suffix.Location = new System.Drawing.Point(276, 3);
            this._suffix.Name = "_suffix";
            this._suffix.Size = new System.Drawing.Size(90, 20);
            this._suffix.TabIndex = 1;
            this._suffix.Text = "CUSTOM";
            //
            // _targets
            //
            this._targets.CheckOnClick = true;
            this._targets.Dock = System.Windows.Forms.DockStyle.Fill;
            this._targets.IntegralHeight = false;
            this._targets.Location = new System.Drawing.Point(3, 295);
            this._targets.Name = "_targets";
            this._targets.Size = new System.Drawing.Size(405, 117);
            this._targets.TabIndex = 3;
            //
            // _npcTypes
            //
            this._npcTypes.CheckOnClick = true;
            this._npcTypes.Dock = System.Windows.Forms.DockStyle.Fill;
            this._npcTypes.IntegralHeight = false;
            this._npcTypes.Location = new System.Drawing.Point(414, 295);
            this._npcTypes.Name = "_npcTypes";
            this._npcTypes.Size = new System.Drawing.Size(268, 117);
            this._npcTypes.TabIndex = 4;
            //
            // _output
            //
            this._output.Dock = System.Windows.Forms.DockStyle.Fill;
            this._output.Font = new System.Drawing.Font("Courier New", 8.5F);
            this._output.Location = new System.Drawing.Point(9, 524);
            this._output.Multiline = true;
            this._output.Name = "_output";
            this._output.ReadOnly = true;
            this._output.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this._output.Size = new System.Drawing.Size(1246, 252);
            this._output.TabIndex = 3;
            this._output.WordWrap = false;
            //
            // _buttons
            //
            this._buttons.AutoSize = true;
            this._buttons.Controls.Add(this._check);
            this._buttons.Controls.Add(this._apply);
            this._buttons.Controls.Add(this._visibility);
            this._buttons.Controls.Add(this._close);
            this._buttons.Controls.Add(this._status);
            this._buttons.Dock = System.Windows.Forms.DockStyle.Fill;
            this._buttons.Location = new System.Drawing.Point(9, 782);
            this._buttons.Name = "_buttons";
            this._buttons.Size = new System.Drawing.Size(1246, 30);
            this._buttons.TabIndex = 4;
            this._buttons.WrapContents = false;
            //
            // _check
            //
            this._check.AutoSize = true;
            this._check.Location = new System.Drawing.Point(3, 3);
            this._check.Name = "_check";
            this._check.Size = new System.Drawing.Size(75, 23);
            this._check.TabIndex = 0;
            this._check.Text = "Check Plan";
            this._check.Click += new System.EventHandler(this.Check_Click);
            //
            // _apply
            //
            this._apply.AutoSize = true;
            this._apply.Location = new System.Drawing.Point(84, 3);
            this._apply.Name = "_apply";
            this._apply.Size = new System.Drawing.Size(75, 23);
            this._apply.TabIndex = 1;
            this._apply.Text = "Replace";
            this._apply.Click += new System.EventHandler(this.Apply_Click);
            //
            // _visibility
            //
            this._visibility.AutoSize = true;
            this._visibility.Location = new System.Drawing.Point(165, 3);
            this._visibility.Name = "_visibility";
            this._visibility.Size = new System.Drawing.Size(106, 23);
            this._visibility.TabIndex = 2;
            this._visibility.Text = "Apply Visibility Only";
            this._visibility.Click += new System.EventHandler(this.Visibility_Click);
            //
            // _close
            //
            this._close.AutoSize = true;
            this._close.Location = new System.Drawing.Point(277, 3);
            this._close.Name = "_close";
            this._close.Size = new System.Drawing.Size(75, 23);
            this._close.TabIndex = 3;
            this._close.Text = "Close";
            this._close.Click += new System.EventHandler(this.Close_Click);
            //
            // _status
            //
            this._status.AutoSize = true;
            this._status.Location = new System.Drawing.Point(358, 0);
            this._status.Name = "_status";
            this._status.Size = new System.Drawing.Size(0, 13);
            this._status.TabIndex = 4;
            //
            // ReplaceWeaponModel
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1264, 821);
            this.Controls.Add(this._root);
            this.KeyPreview = true;
            this.MinimumSize = new System.Drawing.Size(900, 600);
            this.Name = "ReplaceWeaponModel";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Replace Weapon Model";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.ReplaceWeaponModel_FormClosed);
            this.Load += new System.EventHandler(this.ReplaceWeaponModel_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ReplaceWeaponModel_KeyDown);
            this._root.ResumeLayout(false);
            this._root.PerformLayout();
            this._top.ResumeLayout(false);
            this._top.PerformLayout();
            this._fitRow.ResumeLayout(false);
            this._fitRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._ox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._oy)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._oz)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._rx)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._ry)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._rz)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._scale)).EndInit();
            this._middle.Panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._middle)).EndInit();
            this._middle.ResumeLayout(false);
            this._left.ResumeLayout(false);
            this._left.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._parts)).EndInit();
            this._npcCaption.ResumeLayout(false);
            this._npcCaption.PerformLayout();
            this._buttons.ResumeLayout(false);
            this._buttons.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel _root;
        private System.Windows.Forms.FlowLayoutPanel _top;
        private System.Windows.Forms.Label _weaponLabel;
        private System.Windows.Forms.ComboBox _weapon;
        private System.Windows.Forms.Label _pathLabel;
        private System.Windows.Forms.TextBox _path;
        private System.Windows.Forms.Button _browse;
        private System.Windows.Forms.Button _reference;
        private System.Windows.Forms.FlowLayoutPanel _fitRow;
        private System.Windows.Forms.Label _oxLabel;
        private System.Windows.Forms.NumericUpDown _ox;
        private System.Windows.Forms.Label _oyLabel;
        private System.Windows.Forms.NumericUpDown _oy;
        private System.Windows.Forms.Label _ozLabel;
        private System.Windows.Forms.NumericUpDown _oz;
        private System.Windows.Forms.Label _rxLabel;
        private System.Windows.Forms.NumericUpDown _rx;
        private System.Windows.Forms.Label _ryLabel;
        private System.Windows.Forms.NumericUpDown _ry;
        private System.Windows.Forms.Label _rzLabel;
        private System.Windows.Forms.NumericUpDown _rz;
        private System.Windows.Forms.Label _scaleLabel;
        private System.Windows.Forms.NumericUpDown _scale;
        private System.Windows.Forms.Label _muzzleLabel;
        private System.Windows.Forms.ComboBox _muzzle;
        private System.Windows.Forms.Label _physicsLabel;
        private System.Windows.Forms.ComboBox _physics;
        private System.Windows.Forms.Label _prefixLabel;
        private System.Windows.Forms.TextBox _prefix;
        private System.Windows.Forms.SplitContainer _middle;
        private System.Windows.Forms.TableLayoutPanel _left;
        private System.Windows.Forms.DataGridView _parts;
        private System.Windows.Forms.DataGridViewTextBoxColumn _partColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn _boneColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn _movedColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn _scriptColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn _shownColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn _actionColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn _meshesColumn;
        private System.Windows.Forms.Label _targetsLabel;
        private System.Windows.Forms.FlowLayoutPanel _npcCaption;
        private System.Windows.Forms.Label _npcLabel;
        private System.Windows.Forms.TextBox _suffix;
        private System.Windows.Forms.CheckedListBox _targets;
        private System.Windows.Forms.CheckedListBox _npcTypes;
        private System.Windows.Forms.TextBox _output;
        private System.Windows.Forms.FlowLayoutPanel _buttons;
        private System.Windows.Forms.Button _check;
        private System.Windows.Forms.Button _apply;
        private System.Windows.Forms.Button _visibility;
        private System.Windows.Forms.Button _close;
        private System.Windows.Forms.Label _status;
    }
}
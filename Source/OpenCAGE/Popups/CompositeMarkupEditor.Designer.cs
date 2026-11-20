namespace OpenCAGE.Popups
{
    partial class CompositeMarkupEditor
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
            this._title = new System.Windows.Forms.Label();
            this._builtIn = new System.Windows.Forms.Label();
            this._split = new System.Windows.Forms.SplitContainer();
            this._pieces = new System.Windows.Forms.ListBox();
            this._listButtons = new System.Windows.Forms.FlowLayoutPanel();
            this._newShape = new System.Windows.Forms.ComboBox();
            this._add = new System.Windows.Forms.Button();
            this._import = new System.Windows.Forms.Button();
            this._duplicate = new System.Windows.Forms.Button();
            this._remove = new System.Windows.Forms.Button();
            this._properties = new System.Windows.Forms.TableLayoutPanel();
            this._nameCaption = new System.Windows.Forms.Label();
            this._name = new System.Windows.Forms.TextBox();
            this._shapeCaption = new System.Windows.Forms.Label();
            this._shape = new System.Windows.Forms.ComboBox();
            this._positionCaption = new System.Windows.Forms.Label();
            this._positionX = new System.Windows.Forms.NumericUpDown();
            this._positionY = new System.Windows.Forms.NumericUpDown();
            this._positionZ = new System.Windows.Forms.NumericUpDown();
            this._rotationCaption = new System.Windows.Forms.Label();
            this._rotationX = new System.Windows.Forms.NumericUpDown();
            this._rotationY = new System.Windows.Forms.NumericUpDown();
            this._rotationZ = new System.Windows.Forms.NumericUpDown();
            this._sizeCaption = new System.Windows.Forms.Label();
            this._sizeX = new System.Windows.Forms.NumericUpDown();
            this._sizeY = new System.Windows.Forms.NumericUpDown();
            this._sizeZ = new System.Windows.Forms.NumericUpDown();
            this._colourCaption = new System.Windows.Forms.Label();
            this._colour = new System.Windows.Forms.Button();
            this._sizeHint = new System.Windows.Forms.Label();
            this._bottom = new System.Windows.Forms.Panel();
            this._status = new System.Windows.Forms.Label();
            this._close = new System.Windows.Forms.Button();
            this._tips = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this._split)).BeginInit();
            this._split.Panel1.SuspendLayout();
            this._split.Panel2.SuspendLayout();
            this._split.SuspendLayout();
            this._listButtons.SuspendLayout();
            this._properties.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._positionX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._positionY)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._positionZ)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._rotationX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._rotationY)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._rotationZ)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._sizeX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._sizeY)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._sizeZ)).BeginInit();
            this._bottom.SuspendLayout();
            this.SuspendLayout();
            //
            // _title
            //
            this._title.AutoEllipsis = true;
            this._title.Dock = System.Windows.Forms.DockStyle.Top;
            this._title.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._title.Location = new System.Drawing.Point(8, 8);
            this._title.Name = "_title";
            this._title.Size = new System.Drawing.Size(668, 20);
            this._title.TabIndex = 0;
            this._title.Text = "Markup";
            //
            // _builtIn
            //
            this._builtIn.Dock = System.Windows.Forms.DockStyle.Top;
            this._builtIn.Location = new System.Drawing.Point(8, 28);
            this._builtIn.Name = "_builtIn";
            this._builtIn.Size = new System.Drawing.Size(668, 34);
            this._builtIn.TabIndex = 1;
            this._builtIn.Text = "No built-in markup.";
            //
            // _split
            //
            this._split.Dock = System.Windows.Forms.DockStyle.Fill;
            this._split.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this._split.Location = new System.Drawing.Point(8, 62);
            this._split.Name = "_split";
            //
            // _split.Panel1
            //
            this._split.Panel1.Controls.Add(this._pieces);
            this._split.Panel1.Controls.Add(this._listButtons);
            //
            // _split.Panel2
            //
            this._split.Panel2.Controls.Add(this._properties);
            this._split.Size = new System.Drawing.Size(668, 314);
            this._split.SplitterDistance = 250;
            this._split.TabIndex = 2;
            //
            // _pieces
            //
            this._pieces.Dock = System.Windows.Forms.DockStyle.Fill;
            this._pieces.FormattingEnabled = true;
            this._pieces.IntegralHeight = false;
            this._pieces.Location = new System.Drawing.Point(0, 0);
            this._pieces.Name = "_pieces";
            this._pieces.Size = new System.Drawing.Size(250, 244);
            this._pieces.TabIndex = 0;
            this._pieces.SelectedIndexChanged += new System.EventHandler(this._pieces_SelectedIndexChanged);
            //
            // _listButtons
            //
            this._listButtons.Controls.Add(this._newShape);
            this._listButtons.Controls.Add(this._add);
            this._listButtons.Controls.Add(this._import);
            this._listButtons.Controls.Add(this._duplicate);
            this._listButtons.Controls.Add(this._remove);
            this._listButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._listButtons.Location = new System.Drawing.Point(0, 244);
            this._listButtons.Name = "_listButtons";
            this._listButtons.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this._listButtons.Size = new System.Drawing.Size(250, 70);
            this._listButtons.TabIndex = 1;
            //
            // _newShape
            //
            this._newShape.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._newShape.FormattingEnabled = true;
            this._newShape.Location = new System.Drawing.Point(3, 7);
            this._newShape.Name = "_newShape";
            this._newShape.Size = new System.Drawing.Size(110, 21);
            this._newShape.TabIndex = 0;
            this._tips.SetToolTip(this._newShape, "The shape Add Shape adds");
            //
            // _add
            //
            this._add.Location = new System.Drawing.Point(119, 7);
            this._add.Name = "_add";
            this._add.Size = new System.Drawing.Size(85, 23);
            this._add.TabIndex = 1;
            this._add.Text = "Add Shape";
            this._tips.SetToolTip(this._add, "Add a piece of the chosen shape to this composite\'s markup");
            this._add.UseVisualStyleBackColor = true;
            this._add.Click += new System.EventHandler(this._add_Click);
            //
            // _import
            //
            this._import.Location = new System.Drawing.Point(3, 36);
            this._import.Name = "_import";
            this._import.Size = new System.Drawing.Size(85, 23);
            this._import.TabIndex = 2;
            this._import.Text = "Import Mesh";
            this._tips.SetToolTip(this._import, "Add a model file\'s triangles (FBX, OBJ, glTF...) as a piece");
            this._import.UseVisualStyleBackColor = true;
            this._import.Click += new System.EventHandler(this._import_Click);
            //
            // _duplicate
            //
            this._duplicate.Location = new System.Drawing.Point(94, 36);
            this._duplicate.Name = "_duplicate";
            this._duplicate.Size = new System.Drawing.Size(70, 23);
            this._duplicate.TabIndex = 3;
            this._duplicate.Text = "Duplicate";
            this._duplicate.UseVisualStyleBackColor = true;
            this._duplicate.Click += new System.EventHandler(this._duplicate_Click);
            //
            // _remove
            //
            this._remove.Location = new System.Drawing.Point(170, 36);
            this._remove.Name = "_remove";
            this._remove.Size = new System.Drawing.Size(70, 23);
            this._remove.TabIndex = 4;
            this._remove.Text = "Remove";
            this._remove.UseVisualStyleBackColor = true;
            this._remove.Click += new System.EventHandler(this._remove_Click);
            //
            // _properties
            //
            this._properties.ColumnCount = 4;
            this._properties.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 64F));
            this._properties.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this._properties.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this._properties.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this._properties.Controls.Add(this._nameCaption, 0, 0);
            this._properties.Controls.Add(this._name, 1, 0);
            this._properties.Controls.Add(this._shapeCaption, 0, 1);
            this._properties.Controls.Add(this._shape, 1, 1);
            this._properties.Controls.Add(this._positionCaption, 0, 2);
            this._properties.Controls.Add(this._positionX, 1, 2);
            this._properties.Controls.Add(this._positionY, 2, 2);
            this._properties.Controls.Add(this._positionZ, 3, 2);
            this._properties.Controls.Add(this._rotationCaption, 0, 3);
            this._properties.Controls.Add(this._rotationX, 1, 3);
            this._properties.Controls.Add(this._rotationY, 2, 3);
            this._properties.Controls.Add(this._rotationZ, 3, 3);
            this._properties.Controls.Add(this._sizeCaption, 0, 4);
            this._properties.Controls.Add(this._sizeX, 1, 4);
            this._properties.Controls.Add(this._sizeY, 2, 4);
            this._properties.Controls.Add(this._sizeZ, 3, 4);
            this._properties.Controls.Add(this._colourCaption, 0, 5);
            this._properties.Controls.Add(this._colour, 1, 5);
            this._properties.Controls.Add(this._sizeHint, 0, 6);
            this._properties.Dock = System.Windows.Forms.DockStyle.Fill;
            this._properties.Location = new System.Drawing.Point(0, 0);
            this._properties.Name = "_properties";
            this._properties.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this._properties.RowCount = 8;
            this._properties.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this._properties.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this._properties.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this._properties.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this._properties.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this._properties.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this._properties.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));
            this._properties.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._properties.Size = new System.Drawing.Size(414, 314);
            this._properties.TabIndex = 0;
            //
            // _nameCaption
            //
            this._nameCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._nameCaption.AutoSize = true;
            this._nameCaption.Location = new System.Drawing.Point(9, 7);
            this._nameCaption.Name = "_nameCaption";
            this._nameCaption.Size = new System.Drawing.Size(35, 13);
            this._nameCaption.TabIndex = 0;
            this._nameCaption.Text = "Name";
            //
            // _name
            //
            this._name.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._properties.SetColumnSpan(this._name, 3);
            this._name.Location = new System.Drawing.Point(73, 4);
            this._name.Name = "_name";
            this._name.Size = new System.Drawing.Size(338, 20);
            this._name.TabIndex = 1;
            this._name.TextChanged += new System.EventHandler(this._field_Changed);
            //
            // _shapeCaption
            //
            this._shapeCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._shapeCaption.AutoSize = true;
            this._shapeCaption.Location = new System.Drawing.Point(9, 35);
            this._shapeCaption.Name = "_shapeCaption";
            this._shapeCaption.Size = new System.Drawing.Size(38, 13);
            this._shapeCaption.TabIndex = 2;
            this._shapeCaption.Text = "Shape";
            //
            // _shape
            //
            this._shape.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._shape.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._shape.FormattingEnabled = true;
            this._shape.Location = new System.Drawing.Point(73, 31);
            this._shape.Name = "_shape";
            this._shape.Size = new System.Drawing.Size(110, 21);
            this._shape.TabIndex = 3;
            this._shape.SelectedIndexChanged += new System.EventHandler(this._field_Changed);
            //
            // _positionCaption
            //
            this._positionCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._positionCaption.AutoSize = true;
            this._positionCaption.Location = new System.Drawing.Point(9, 63);
            this._positionCaption.Name = "_positionCaption";
            this._positionCaption.Size = new System.Drawing.Size(44, 13);
            this._positionCaption.TabIndex = 4;
            this._positionCaption.Text = "Position";
            //
            // _positionX
            //
            this._positionX.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._positionX.DecimalPlaces = 3;
            this._positionX.Increment = new decimal(new int[] { 5, 0, 0, 131072 });
            this._positionX.Location = new System.Drawing.Point(73, 59);
            this._positionX.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            this._positionX.Minimum = new decimal(new int[] { 1000, 0, 0, -2147483648 });
            this._positionX.Name = "_positionX";
            this._positionX.Size = new System.Drawing.Size(107, 20);
            this._positionX.TabIndex = 5;
            this._tips.SetToolTip(this._positionX, "X (metres), in the composite\'s space");
            this._positionX.ValueChanged += new System.EventHandler(this._field_Changed);
            //
            // _positionY
            //
            this._positionY.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._positionY.DecimalPlaces = 3;
            this._positionY.Increment = new decimal(new int[] { 5, 0, 0, 131072 });
            this._positionY.Location = new System.Drawing.Point(186, 59);
            this._positionY.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            this._positionY.Minimum = new decimal(new int[] { 1000, 0, 0, -2147483648 });
            this._positionY.Name = "_positionY";
            this._positionY.Size = new System.Drawing.Size(107, 20);
            this._positionY.TabIndex = 6;
            this._tips.SetToolTip(this._positionY, "Y (metres, up)");
            this._positionY.ValueChanged += new System.EventHandler(this._field_Changed);
            //
            // _positionZ
            //
            this._positionZ.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._positionZ.DecimalPlaces = 3;
            this._positionZ.Increment = new decimal(new int[] { 5, 0, 0, 131072 });
            this._positionZ.Location = new System.Drawing.Point(299, 59);
            this._positionZ.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            this._positionZ.Minimum = new decimal(new int[] { 1000, 0, 0, -2147483648 });
            this._positionZ.Name = "_positionZ";
            this._positionZ.Size = new System.Drawing.Size(112, 20);
            this._positionZ.TabIndex = 7;
            this._tips.SetToolTip(this._positionZ, "Z (metres, the composite\'s forward)");
            this._positionZ.ValueChanged += new System.EventHandler(this._field_Changed);
            //
            // _rotationCaption
            //
            this._rotationCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._rotationCaption.AutoSize = true;
            this._rotationCaption.Location = new System.Drawing.Point(9, 91);
            this._rotationCaption.Name = "_rotationCaption";
            this._rotationCaption.Size = new System.Drawing.Size(47, 13);
            this._rotationCaption.TabIndex = 8;
            this._rotationCaption.Text = "Rotation";
            //
            // _rotationX
            //
            this._rotationX.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._rotationX.DecimalPlaces = 1;
            this._rotationX.Increment = new decimal(new int[] { 5, 0, 0, 0 });
            this._rotationX.Location = new System.Drawing.Point(73, 87);
            this._rotationX.Maximum = new decimal(new int[] { 360, 0, 0, 0 });
            this._rotationX.Minimum = new decimal(new int[] { 360, 0, 0, -2147483648 });
            this._rotationX.Name = "_rotationX";
            this._rotationX.Size = new System.Drawing.Size(107, 20);
            this._rotationX.TabIndex = 9;
            this._tips.SetToolTip(this._rotationX, "Pitch (degrees)");
            this._rotationX.ValueChanged += new System.EventHandler(this._field_Changed);
            //
            // _rotationY
            //
            this._rotationY.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._rotationY.DecimalPlaces = 1;
            this._rotationY.Increment = new decimal(new int[] { 5, 0, 0, 0 });
            this._rotationY.Location = new System.Drawing.Point(186, 87);
            this._rotationY.Maximum = new decimal(new int[] { 360, 0, 0, 0 });
            this._rotationY.Minimum = new decimal(new int[] { 360, 0, 0, -2147483648 });
            this._rotationY.Name = "_rotationY";
            this._rotationY.Size = new System.Drawing.Size(107, 20);
            this._rotationY.TabIndex = 10;
            this._tips.SetToolTip(this._rotationY, "Yaw (degrees, about up)");
            this._rotationY.ValueChanged += new System.EventHandler(this._field_Changed);
            //
            // _rotationZ
            //
            this._rotationZ.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._rotationZ.DecimalPlaces = 1;
            this._rotationZ.Increment = new decimal(new int[] { 5, 0, 0, 0 });
            this._rotationZ.Location = new System.Drawing.Point(299, 87);
            this._rotationZ.Maximum = new decimal(new int[] { 360, 0, 0, 0 });
            this._rotationZ.Minimum = new decimal(new int[] { 360, 0, 0, -2147483648 });
            this._rotationZ.Name = "_rotationZ";
            this._rotationZ.Size = new System.Drawing.Size(112, 20);
            this._rotationZ.TabIndex = 11;
            this._tips.SetToolTip(this._rotationZ, "Roll (degrees)");
            this._rotationZ.ValueChanged += new System.EventHandler(this._field_Changed);
            //
            // _sizeCaption
            //
            this._sizeCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._sizeCaption.AutoSize = true;
            this._sizeCaption.Location = new System.Drawing.Point(9, 119);
            this._sizeCaption.Name = "_sizeCaption";
            this._sizeCaption.Size = new System.Drawing.Size(27, 13);
            this._sizeCaption.TabIndex = 12;
            this._sizeCaption.Text = "Size";
            //
            // _sizeX
            //
            this._sizeX.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._sizeX.DecimalPlaces = 3;
            this._sizeX.Increment = new decimal(new int[] { 5, 0, 0, 131072 });
            this._sizeX.Location = new System.Drawing.Point(73, 115);
            this._sizeX.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            this._sizeX.Minimum = new decimal(new int[] { 1, 0, 0, 196608 });
            this._sizeX.Name = "_sizeX";
            this._sizeX.Size = new System.Drawing.Size(107, 20);
            this._sizeX.TabIndex = 13;
            this._sizeX.Value = new decimal(new int[] { 1, 0, 0, 0 });
            this._sizeX.ValueChanged += new System.EventHandler(this._field_Changed);
            //
            // _sizeY
            //
            this._sizeY.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._sizeY.DecimalPlaces = 3;
            this._sizeY.Increment = new decimal(new int[] { 5, 0, 0, 131072 });
            this._sizeY.Location = new System.Drawing.Point(186, 115);
            this._sizeY.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            this._sizeY.Minimum = new decimal(new int[] { 1, 0, 0, 196608 });
            this._sizeY.Name = "_sizeY";
            this._sizeY.Size = new System.Drawing.Size(107, 20);
            this._sizeY.TabIndex = 14;
            this._sizeY.Value = new decimal(new int[] { 1, 0, 0, 0 });
            this._sizeY.ValueChanged += new System.EventHandler(this._field_Changed);
            //
            // _sizeZ
            //
            this._sizeZ.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._sizeZ.DecimalPlaces = 3;
            this._sizeZ.Increment = new decimal(new int[] { 5, 0, 0, 131072 });
            this._sizeZ.Location = new System.Drawing.Point(299, 115);
            this._sizeZ.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            this._sizeZ.Minimum = new decimal(new int[] { 1, 0, 0, 196608 });
            this._sizeZ.Name = "_sizeZ";
            this._sizeZ.Size = new System.Drawing.Size(112, 20);
            this._sizeZ.TabIndex = 15;
            this._sizeZ.Value = new decimal(new int[] { 1, 0, 0, 0 });
            this._sizeZ.ValueChanged += new System.EventHandler(this._field_Changed);
            //
            // _colourCaption
            //
            this._colourCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._colourCaption.AutoSize = true;
            this._colourCaption.Location = new System.Drawing.Point(9, 148);
            this._colourCaption.Name = "_colourCaption";
            this._colourCaption.Size = new System.Drawing.Size(37, 13);
            this._colourCaption.TabIndex = 16;
            this._colourCaption.Text = "Colour";
            //
            // _colour
            //
            this._colour.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._colour.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._colour.Location = new System.Drawing.Point(73, 143);
            this._colour.Name = "_colour";
            this._colour.Size = new System.Drawing.Size(60, 23);
            this._colour.TabIndex = 17;
            this._tips.SetToolTip(this._colour, "Pick the piece\'s colour");
            this._colour.UseVisualStyleBackColor = false;
            this._colour.Click += new System.EventHandler(this._colour_Click);
            //
            // _sizeHint
            //
            this._properties.SetColumnSpan(this._sizeHint, 4);
            this._sizeHint.Dock = System.Windows.Forms.DockStyle.Fill;
            this._sizeHint.Location = new System.Drawing.Point(9, 170);
            this._sizeHint.Name = "_sizeHint";
            this._sizeHint.Size = new System.Drawing.Size(402, 60);
            this._sizeHint.TabIndex = 18;
            this._sizeHint.Text = "Select a piece to edit it.";
            //
            // _bottom
            //
            this._bottom.Controls.Add(this._status);
            this._bottom.Controls.Add(this._close);
            this._bottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._bottom.Location = new System.Drawing.Point(8, 376);
            this._bottom.Name = "_bottom";
            this._bottom.Size = new System.Drawing.Size(668, 36);
            this._bottom.TabIndex = 3;
            //
            // _status
            //
            this._status.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this._status.AutoEllipsis = true;
            this._status.Location = new System.Drawing.Point(0, 10);
            this._status.Name = "_status";
            this._status.Size = new System.Drawing.Size(578, 18);
            this._status.TabIndex = 0;
            //
            // _close
            //
            this._close.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this._close.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._close.Location = new System.Drawing.Point(588, 6);
            this._close.Name = "_close";
            this._close.Size = new System.Drawing.Size(80, 26);
            this._close.TabIndex = 1;
            this._close.Text = "Close";
            this._close.UseVisualStyleBackColor = true;
            this._close.Click += new System.EventHandler(this._close_Click);
            //
            // CompositeMarkupEditor
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._close;
            this.ClientSize = new System.Drawing.Size(684, 420);
            this.Controls.Add(this._split);
            this.Controls.Add(this._bottom);
            this.Controls.Add(this._builtIn);
            this.Controls.Add(this._title);
            this.KeyPreview = true;
            this.MinimumSize = new System.Drawing.Size(600, 400);
            this.Name = "CompositeMarkupEditor";
            this.Padding = new System.Windows.Forms.Padding(8);
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Markup";
            this._split.Panel1.ResumeLayout(false);
            this._split.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._split)).EndInit();
            this._split.ResumeLayout(false);
            this._listButtons.ResumeLayout(false);
            this._properties.ResumeLayout(false);
            this._properties.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._positionX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._positionY)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._positionZ)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._rotationX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._rotationY)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._rotationZ)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._sizeX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._sizeY)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._sizeZ)).EndInit();
            this._bottom.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label _title;
        private System.Windows.Forms.Label _builtIn;
        private System.Windows.Forms.SplitContainer _split;
        private System.Windows.Forms.ListBox _pieces;
        private System.Windows.Forms.FlowLayoutPanel _listButtons;
        private System.Windows.Forms.ComboBox _newShape;
        private System.Windows.Forms.Button _add;
        private System.Windows.Forms.Button _import;
        private System.Windows.Forms.Button _duplicate;
        private System.Windows.Forms.Button _remove;
        private System.Windows.Forms.TableLayoutPanel _properties;
        private System.Windows.Forms.Label _nameCaption;
        private System.Windows.Forms.TextBox _name;
        private System.Windows.Forms.Label _shapeCaption;
        private System.Windows.Forms.ComboBox _shape;
        private System.Windows.Forms.Label _positionCaption;
        private System.Windows.Forms.NumericUpDown _positionX;
        private System.Windows.Forms.NumericUpDown _positionY;
        private System.Windows.Forms.NumericUpDown _positionZ;
        private System.Windows.Forms.Label _rotationCaption;
        private System.Windows.Forms.NumericUpDown _rotationX;
        private System.Windows.Forms.NumericUpDown _rotationY;
        private System.Windows.Forms.NumericUpDown _rotationZ;
        private System.Windows.Forms.Label _sizeCaption;
        private System.Windows.Forms.NumericUpDown _sizeX;
        private System.Windows.Forms.NumericUpDown _sizeY;
        private System.Windows.Forms.NumericUpDown _sizeZ;
        private System.Windows.Forms.Label _colourCaption;
        private System.Windows.Forms.Button _colour;
        private System.Windows.Forms.Label _sizeHint;
        private System.Windows.Forms.Panel _bottom;
        private System.Windows.Forms.Label _status;
        private System.Windows.Forms.Button _close;
        private System.Windows.Forms.ToolTip _tips;
    }
}

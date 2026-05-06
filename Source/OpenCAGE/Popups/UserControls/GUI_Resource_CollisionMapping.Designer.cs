namespace OpenCAGE.Popups.UserControls
{
    partial class GUI_Resource_CollisionMapping
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label8 = new System.Windows.Forms.Label();
            this.groupBoxFlags = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.groupBoxState = new System.Windows.Forms.GroupBox();
            this.groupBoxType = new System.Windows.Forms.GroupBox();
            this.groupBoxSource = new System.Windows.Forms.GroupBox();
            this.groupBoxStorage = new System.Windows.Forms.GroupBox();
            this.groupBoxMotion = new System.Windows.Forms.GroupBox();
            this.label10 = new System.Windows.Forms.Label();
            this.groupBoxMaterial = new System.Windows.Forms.GroupBox();
            this.materialMappingName = new System.Windows.Forms.TextBox();
            this.materialName = new System.Windows.Forms.TextBox();
            this.btnClearMaterialMapping = new System.Windows.Forms.Button();
            this.btnSetMaterialMapping = new System.Windows.Forms.Button();
            this.labelMaterialMapping = new System.Windows.Forms.Label();
            this.btnSetMaterial = new System.Windows.Forms.Button();
            this.labelMaterial = new System.Windows.Forms.Label();
            this.ROT_Z = new SmoothNumericUpDown();
            this.groupBoxProperties = new System.Windows.Forms.GroupBox();
            this.labelCollisionProxyIndex = new System.Windows.Forms.Label();
            this.numericCollisionProxyIndex = new System.Windows.Forms.NumericUpDown();
            this.labelIndex = new System.Windows.Forms.Label();
            this.numericIndex = new System.Windows.Forms.NumericUpDown();
            this.label11 = new System.Windows.Forms.Label();
            this.POS_X = new SmoothNumericUpDown();
            this.ROT_Y = new SmoothNumericUpDown();
            this.POS_Z = new SmoothNumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.POS_Y = new SmoothNumericUpDown();
            this.ROT_X = new SmoothNumericUpDown();
            this.label7 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.groupBoxFlags.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.groupBoxMaterial.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ROT_Z)).BeginInit();
            this.groupBoxProperties.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericCollisionProxyIndex)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericIndex)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_X)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ROT_Y)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_Z)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_Y)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ROT_X)).BeginInit();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label8);
            this.groupBox1.Controls.Add(this.groupBoxFlags);
            this.groupBox1.Controls.Add(this.label10);
            this.groupBox1.Controls.Add(this.groupBoxMaterial);
            this.groupBox1.Controls.Add(this.ROT_Z);
            this.groupBox1.Controls.Add(this.groupBoxProperties);
            this.groupBox1.Controls.Add(this.label11);
            this.groupBox1.Controls.Add(this.POS_X);
            this.groupBox1.Controls.Add(this.ROT_Y);
            this.groupBox1.Controls.Add(this.POS_Z);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.label12);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.POS_Y);
            this.groupBox1.Controls.Add(this.ROT_X);
            this.groupBox1.Controls.Add(this.label7);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Location = new System.Drawing.Point(3, 3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(832, 394);
            this.groupBox1.TabIndex = 3;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Collision Mapping";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(448, 337);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(47, 13);
            this.label8.TabIndex = 42;
            this.label8.Text = "Rotation";
            // 
            // groupBoxFlags
            // 
            this.groupBoxFlags.Controls.Add(this.tableLayoutPanel1);
            this.groupBoxFlags.Location = new System.Drawing.Point(6, 19);
            this.groupBoxFlags.Name = "groupBoxFlags";
            this.groupBoxFlags.Size = new System.Drawing.Size(823, 200);
            this.groupBoxFlags.TabIndex = 4;
            this.groupBoxFlags.TabStop = false;
            this.groupBoxFlags.Text = "Collision Flags";
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 5;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel1.Controls.Add(this.groupBoxState, 4, 0);
            this.tableLayoutPanel1.Controls.Add(this.groupBoxType, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.groupBoxSource, 3, 0);
            this.tableLayoutPanel1.Controls.Add(this.groupBoxStorage, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.groupBoxMotion, 2, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(3, 16);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(817, 181);
            this.tableLayoutPanel1.TabIndex = 5;
            // 
            // groupBoxState
            // 
            this.groupBoxState.Location = new System.Drawing.Point(655, 3);
            this.groupBoxState.Name = "groupBoxState";
            this.groupBoxState.Size = new System.Drawing.Size(159, 175);
            this.groupBoxState.TabIndex = 4;
            this.groupBoxState.TabStop = false;
            this.groupBoxState.Text = "State";
            // 
            // groupBoxType
            // 
            this.groupBoxType.Location = new System.Drawing.Point(3, 3);
            this.groupBoxType.Name = "groupBoxType";
            this.groupBoxType.Size = new System.Drawing.Size(157, 175);
            this.groupBoxType.TabIndex = 0;
            this.groupBoxType.TabStop = false;
            this.groupBoxType.Text = "Type";
            // 
            // groupBoxSource
            // 
            this.groupBoxSource.Location = new System.Drawing.Point(492, 3);
            this.groupBoxSource.Name = "groupBoxSource";
            this.groupBoxSource.Size = new System.Drawing.Size(157, 175);
            this.groupBoxSource.TabIndex = 3;
            this.groupBoxSource.TabStop = false;
            this.groupBoxSource.Text = "Source";
            // 
            // groupBoxStorage
            // 
            this.groupBoxStorage.Location = new System.Drawing.Point(166, 3);
            this.groupBoxStorage.Name = "groupBoxStorage";
            this.groupBoxStorage.Size = new System.Drawing.Size(157, 175);
            this.groupBoxStorage.TabIndex = 1;
            this.groupBoxStorage.TabStop = false;
            this.groupBoxStorage.Text = "Storage";
            // 
            // groupBoxMotion
            // 
            this.groupBoxMotion.Location = new System.Drawing.Point(329, 3);
            this.groupBoxMotion.Name = "groupBoxMotion";
            this.groupBoxMotion.Size = new System.Drawing.Size(157, 175);
            this.groupBoxMotion.TabIndex = 2;
            this.groupBoxMotion.TabStop = false;
            this.groupBoxMotion.Text = "Motion";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(654, 356);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(17, 13);
            this.label10.TabIndex = 41;
            this.label10.Text = "Z:";
            // 
            // groupBoxMaterial
            // 
            this.groupBoxMaterial.Controls.Add(this.materialMappingName);
            this.groupBoxMaterial.Controls.Add(this.materialName);
            this.groupBoxMaterial.Controls.Add(this.btnClearMaterialMapping);
            this.groupBoxMaterial.Controls.Add(this.btnSetMaterialMapping);
            this.groupBoxMaterial.Controls.Add(this.labelMaterialMapping);
            this.groupBoxMaterial.Controls.Add(this.btnSetMaterial);
            this.groupBoxMaterial.Controls.Add(this.labelMaterial);
            this.groupBoxMaterial.Location = new System.Drawing.Point(406, 225);
            this.groupBoxMaterial.Name = "groupBoxMaterial";
            this.groupBoxMaterial.Size = new System.Drawing.Size(426, 100);
            this.groupBoxMaterial.TabIndex = 6;
            this.groupBoxMaterial.TabStop = false;
            this.groupBoxMaterial.Text = "Material";
            // 
            // materialMappingName
            // 
            this.materialMappingName.Location = new System.Drawing.Point(103, 48);
            this.materialMappingName.Name = "materialMappingName";
            this.materialMappingName.ReadOnly = true;
            this.materialMappingName.Size = new System.Drawing.Size(212, 20);
            this.materialMappingName.TabIndex = 6;
            // 
            // materialName
            // 
            this.materialName.Location = new System.Drawing.Point(102, 24);
            this.materialName.Name = "materialName";
            this.materialName.ReadOnly = true;
            this.materialName.Size = new System.Drawing.Size(212, 20);
            this.materialName.TabIndex = 5;
            // 
            // btnClearMaterialMapping
            // 
            this.btnClearMaterialMapping.Enabled = false;
            this.btnClearMaterialMapping.Location = new System.Drawing.Point(345, 71);
            this.btnClearMaterialMapping.Name = "btnClearMaterialMapping";
            this.btnClearMaterialMapping.Size = new System.Drawing.Size(75, 23);
            this.btnClearMaterialMapping.TabIndex = 4;
            this.btnClearMaterialMapping.Text = "Clear";
            this.btnClearMaterialMapping.UseVisualStyleBackColor = true;
            this.btnClearMaterialMapping.Click += new System.EventHandler(this.btnClearMaterialMapping_Click);
            // 
            // btnSetMaterialMapping
            // 
            this.btnSetMaterialMapping.Enabled = false;
            this.btnSetMaterialMapping.Location = new System.Drawing.Point(321, 47);
            this.btnSetMaterialMapping.Name = "btnSetMaterialMapping";
            this.btnSetMaterialMapping.Size = new System.Drawing.Size(100, 23);
            this.btnSetMaterialMapping.TabIndex = 3;
            this.btnSetMaterialMapping.Text = "Set Mapping...";
            this.btnSetMaterialMapping.UseVisualStyleBackColor = true;
            this.btnSetMaterialMapping.Click += new System.EventHandler(this.btnSetMaterialMapping_Click);
            // 
            // labelMaterialMapping
            // 
            this.labelMaterialMapping.AutoSize = true;
            this.labelMaterialMapping.Location = new System.Drawing.Point(6, 50);
            this.labelMaterialMapping.Name = "labelMaterialMapping";
            this.labelMaterialMapping.Size = new System.Drawing.Size(91, 13);
            this.labelMaterialMapping.TabIndex = 2;
            this.labelMaterialMapping.Text = "Material Mapping:";
            // 
            // btnSetMaterial
            // 
            this.btnSetMaterial.Enabled = false;
            this.btnSetMaterial.Location = new System.Drawing.Point(320, 22);
            this.btnSetMaterial.Name = "btnSetMaterial";
            this.btnSetMaterial.Size = new System.Drawing.Size(100, 23);
            this.btnSetMaterial.TabIndex = 1;
            this.btnSetMaterial.Text = "Set Material...";
            this.btnSetMaterial.UseVisualStyleBackColor = true;
            this.btnSetMaterial.Click += new System.EventHandler(this.btnSetMaterial_Click);
            // 
            // labelMaterial
            // 
            this.labelMaterial.AutoSize = true;
            this.labelMaterial.Location = new System.Drawing.Point(49, 27);
            this.labelMaterial.Name = "labelMaterial";
            this.labelMaterial.Size = new System.Drawing.Size(47, 13);
            this.labelMaterial.TabIndex = 0;
            this.labelMaterial.Text = "Material:";
            // 
            // ROT_Z
            // 
            this.ROT_Z.DecimalPlaces = 8;
            this.ROT_Z.Increment = new decimal(new int[] {
            1,
            0,
            0,
            851968});
            this.ROT_Z.Location = new System.Drawing.Point(674, 353);
            this.ROT_Z.Maximum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            0});
            this.ROT_Z.Minimum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.ROT_Z.Name = "ROT_Z";
            this.ROT_Z.Size = new System.Drawing.Size(80, 20);
            this.ROT_Z.TabIndex = 40;
            this.ROT_Z.Value = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.ROT_Z.ValueChanged += new System.EventHandler(this.ROT_Z_ValueChanged);
            // 
            // groupBoxProperties
            // 
            this.groupBoxProperties.Controls.Add(this.labelCollisionProxyIndex);
            this.groupBoxProperties.Controls.Add(this.numericCollisionProxyIndex);
            this.groupBoxProperties.Controls.Add(this.labelIndex);
            this.groupBoxProperties.Controls.Add(this.numericIndex);
            this.groupBoxProperties.Location = new System.Drawing.Point(6, 225);
            this.groupBoxProperties.Name = "groupBoxProperties";
            this.groupBoxProperties.Size = new System.Drawing.Size(394, 54);
            this.groupBoxProperties.TabIndex = 5;
            this.groupBoxProperties.TabStop = false;
            this.groupBoxProperties.Text = "Properties";
            // 
            // labelCollisionProxyIndex
            // 
            this.labelCollisionProxyIndex.AutoSize = true;
            this.labelCollisionProxyIndex.Location = new System.Drawing.Point(183, 22);
            this.labelCollisionProxyIndex.Name = "labelCollisionProxyIndex";
            this.labelCollisionProxyIndex.Size = new System.Drawing.Size(106, 13);
            this.labelCollisionProxyIndex.TabIndex = 2;
            this.labelCollisionProxyIndex.Text = "Collision Proxy Index:";
            // 
            // numericCollisionProxyIndex
            // 
            this.numericCollisionProxyIndex.Enabled = false;
            this.numericCollisionProxyIndex.Location = new System.Drawing.Point(304, 20);
            this.numericCollisionProxyIndex.Maximum = new decimal(new int[] {
            32767,
            0,
            0,
            0});
            this.numericCollisionProxyIndex.Minimum = new decimal(new int[] {
            -32768,
            0,
            0,
            -2147483648});
            this.numericCollisionProxyIndex.Name = "numericCollisionProxyIndex";
            this.numericCollisionProxyIndex.Size = new System.Drawing.Size(73, 20);
            this.numericCollisionProxyIndex.TabIndex = 3;
            // 
            // labelIndex
            // 
            this.labelIndex.AutoSize = true;
            this.labelIndex.Location = new System.Drawing.Point(6, 22);
            this.labelIndex.Name = "labelIndex";
            this.labelIndex.Size = new System.Drawing.Size(36, 13);
            this.labelIndex.TabIndex = 0;
            this.labelIndex.Text = "Index:";
            // 
            // numericIndex
            // 
            this.numericIndex.Enabled = false;
            this.numericIndex.Location = new System.Drawing.Point(48, 20);
            this.numericIndex.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.numericIndex.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.numericIndex.Name = "numericIndex";
            this.numericIndex.Size = new System.Drawing.Size(120, 20);
            this.numericIndex.TabIndex = 1;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(551, 356);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(17, 13);
            this.label11.TabIndex = 39;
            this.label11.Text = "Y:";
            // 
            // POS_X
            // 
            this.POS_X.DecimalPlaces = 8;
            this.POS_X.Increment = new decimal(new int[] {
            1,
            0,
            0,
            851968});
            this.POS_X.Location = new System.Drawing.Point(143, 353);
            this.POS_X.Maximum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            0});
            this.POS_X.Minimum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.POS_X.Name = "POS_X";
            this.POS_X.Size = new System.Drawing.Size(80, 20);
            this.POS_X.TabIndex = 29;
            this.POS_X.Value = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.POS_X.ValueChanged += new System.EventHandler(this.POS_X_ValueChanged);
            // 
            // ROT_Y
            // 
            this.ROT_Y.DecimalPlaces = 8;
            this.ROT_Y.Increment = new decimal(new int[] {
            1,
            0,
            0,
            851968});
            this.ROT_Y.Location = new System.Drawing.Point(570, 353);
            this.ROT_Y.Maximum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            0});
            this.ROT_Y.Minimum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.ROT_Y.Name = "ROT_Y";
            this.ROT_Y.Size = new System.Drawing.Size(80, 20);
            this.ROT_Y.TabIndex = 38;
            this.ROT_Y.Value = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.ROT_Y.ValueChanged += new System.EventHandler(this.ROT_Y_ValueChanged);
            // 
            // POS_Z
            // 
            this.POS_Z.DecimalPlaces = 8;
            this.POS_Z.Increment = new decimal(new int[] {
            1,
            0,
            0,
            851968});
            this.POS_Z.Location = new System.Drawing.Point(350, 353);
            this.POS_Z.Maximum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            0});
            this.POS_Z.Minimum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.POS_Z.Name = "POS_Z";
            this.POS_Z.Size = new System.Drawing.Size(80, 20);
            this.POS_Z.TabIndex = 33;
            this.POS_Z.Value = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.POS_Z.ValueChanged += new System.EventHandler(this.POS_Z_ValueChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(227, 356);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(17, 13);
            this.label3.TabIndex = 32;
            this.label3.Text = "Y:";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(448, 356);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(17, 13);
            this.label12.TabIndex = 37;
            this.label12.Text = "X:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(330, 356);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(17, 13);
            this.label5.TabIndex = 34;
            this.label5.Text = "Z:";
            // 
            // POS_Y
            // 
            this.POS_Y.DecimalPlaces = 8;
            this.POS_Y.Increment = new decimal(new int[] {
            1,
            0,
            0,
            851968});
            this.POS_Y.Location = new System.Drawing.Point(246, 353);
            this.POS_Y.Maximum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            0});
            this.POS_Y.Minimum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.POS_Y.Name = "POS_Y";
            this.POS_Y.Size = new System.Drawing.Size(80, 20);
            this.POS_Y.TabIndex = 31;
            this.POS_Y.Value = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.POS_Y.ValueChanged += new System.EventHandler(this.POS_Y_ValueChanged);
            // 
            // ROT_X
            // 
            this.ROT_X.DecimalPlaces = 8;
            this.ROT_X.Increment = new decimal(new int[] {
            1,
            0,
            0,
            851968});
            this.ROT_X.Location = new System.Drawing.Point(467, 353);
            this.ROT_X.Maximum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            0});
            this.ROT_X.Minimum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.ROT_X.Name = "ROT_X";
            this.ROT_X.Size = new System.Drawing.Size(80, 20);
            this.ROT_X.TabIndex = 36;
            this.ROT_X.Value = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.ROT_X.ValueChanged += new System.EventHandler(this.ROT_X_ValueChanged);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(124, 337);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(44, 13);
            this.label7.TabIndex = 35;
            this.label7.Text = "Position";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(124, 356);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(17, 13);
            this.label2.TabIndex = 30;
            this.label2.Text = "X:";
            // 
            // GUI_Resource_CollisionMapping
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBox1);
            this.Name = "GUI_Resource_CollisionMapping";
            this.Size = new System.Drawing.Size(838, 400);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBoxFlags.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.groupBoxMaterial.ResumeLayout(false);
            this.groupBoxMaterial.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ROT_Z)).EndInit();
            this.groupBoxProperties.ResumeLayout(false);
            this.groupBoxProperties.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericCollisionProxyIndex)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericIndex)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_X)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ROT_Y)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_Z)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_Y)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ROT_X)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label10;
        private SmoothNumericUpDown ROT_Z;
        private System.Windows.Forms.Label label11;
        private SmoothNumericUpDown ROT_Y;
        private System.Windows.Forms.Label label12;
        private SmoothNumericUpDown ROT_X;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label5;
        private SmoothNumericUpDown POS_Z;
        private System.Windows.Forms.Label label3;
        private SmoothNumericUpDown POS_Y;
        private System.Windows.Forms.Label label2;
        private SmoothNumericUpDown POS_X;
        private System.Windows.Forms.GroupBox groupBoxFlags;
        private System.Windows.Forms.GroupBox groupBoxType;
        private System.Windows.Forms.GroupBox groupBoxStorage;
        private System.Windows.Forms.GroupBox groupBoxMotion;
        private System.Windows.Forms.GroupBox groupBoxSource;
        private System.Windows.Forms.GroupBox groupBoxState;
        private System.Windows.Forms.GroupBox groupBoxProperties;
        private System.Windows.Forms.Label labelIndex;
        private System.Windows.Forms.NumericUpDown numericIndex;
        private System.Windows.Forms.Label labelCollisionProxyIndex;
        private System.Windows.Forms.NumericUpDown numericCollisionProxyIndex;
        private System.Windows.Forms.GroupBox groupBoxMaterial;
        private System.Windows.Forms.Label labelMaterial;
        private System.Windows.Forms.Button btnSetMaterial;
        private System.Windows.Forms.Label labelMaterialMapping;
        private System.Windows.Forms.Button btnSetMaterialMapping;
        private System.Windows.Forms.Button btnClearMaterialMapping;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TextBox materialMappingName;
        private System.Windows.Forms.TextBox materialName;
    }
}

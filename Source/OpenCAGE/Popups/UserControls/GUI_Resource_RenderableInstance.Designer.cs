namespace OpenCAGE.Popups.UserControls
{
    partial class GUI_Resource_RenderableInstance
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
            this.label10 = new System.Windows.Forms.Label();
            this.ROT_Z = new SmoothNumericUpDown();
            this.label11 = new System.Windows.Forms.Label();
            this.ROT_Y = new SmoothNumericUpDown();
            this.label12 = new System.Windows.Forms.Label();
            this.ROT_X = new SmoothNumericUpDown();
            this.label7 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.POS_Z = new SmoothNumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.POS_Y = new SmoothNumericUpDown();
            this.label4 = new System.Windows.Forms.Label();
            this.POS_X = new SmoothNumericUpDown();
            this.materials = new System.Windows.Forms.ListBox();
            this.editMaterial = new System.Windows.Forms.Button();
            this.editModel = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.modelInfoTextbox = new System.Windows.Forms.TextBox();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ROT_Z)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ROT_Y)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ROT_X)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_Z)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_Y)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_X)).BeginInit();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label8);
            this.groupBox1.Controls.Add(this.label10);
            this.groupBox1.Controls.Add(this.ROT_Z);
            this.groupBox1.Controls.Add(this.label11);
            this.groupBox1.Controls.Add(this.ROT_Y);
            this.groupBox1.Controls.Add(this.label12);
            this.groupBox1.Controls.Add(this.ROT_X);
            this.groupBox1.Controls.Add(this.label7);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.POS_Z);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.POS_Y);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.POS_X);
            this.groupBox1.Controls.Add(this.materials);
            this.groupBox1.Controls.Add(this.editMaterial);
            this.groupBox1.Controls.Add(this.editModel);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.modelInfoTextbox);
            this.groupBox1.Location = new System.Drawing.Point(3, 3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(832, 227);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Renderable Instance";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(447, 179);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(47, 13);
            this.label8.TabIndex = 159;
            this.label8.Text = "Rotation";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(653, 198);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(17, 13);
            this.label10.TabIndex = 158;
            this.label10.Text = "Z:";
            // 
            // ROT_Z
            // 
            this.ROT_Z.DecimalPlaces = 8;
            this.ROT_Z.Increment = new decimal(new int[] {
            1,
            0,
            0,
            851968});
            this.ROT_Z.Location = new System.Drawing.Point(673, 195);
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
            this.ROT_Z.TabIndex = 157;
            this.ROT_Z.Value = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.ROT_Z.ValueChanged += new System.EventHandler(this.ROT_Z_ValueChanged);
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(550, 198);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(17, 13);
            this.label11.TabIndex = 156;
            this.label11.Text = "Y:";
            // 
            // ROT_Y
            // 
            this.ROT_Y.DecimalPlaces = 8;
            this.ROT_Y.Increment = new decimal(new int[] {
            1,
            0,
            0,
            851968});
            this.ROT_Y.Location = new System.Drawing.Point(569, 195);
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
            this.ROT_Y.TabIndex = 155;
            this.ROT_Y.Value = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.ROT_Y.ValueChanged += new System.EventHandler(this.ROT_Y_ValueChanged);
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(447, 198);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(17, 13);
            this.label12.TabIndex = 154;
            this.label12.Text = "X:";
            // 
            // ROT_X
            // 
            this.ROT_X.DecimalPlaces = 8;
            this.ROT_X.Increment = new decimal(new int[] {
            1,
            0,
            0,
            851968});
            this.ROT_X.Location = new System.Drawing.Point(466, 195);
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
            this.ROT_X.TabIndex = 153;
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
            this.label7.Location = new System.Drawing.Point(58, 179);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(44, 13);
            this.label7.TabIndex = 152;
            this.label7.Text = "Position";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(264, 198);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(17, 13);
            this.label5.TabIndex = 151;
            this.label5.Text = "Z:";
            // 
            // POS_Z
            // 
            this.POS_Z.DecimalPlaces = 8;
            this.POS_Z.Increment = new decimal(new int[] {
            1,
            0,
            0,
            851968});
            this.POS_Z.Location = new System.Drawing.Point(284, 195);
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
            this.POS_Z.TabIndex = 150;
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
            this.label3.Location = new System.Drawing.Point(161, 198);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(17, 13);
            this.label3.TabIndex = 149;
            this.label3.Text = "Y:";
            // 
            // POS_Y
            // 
            this.POS_Y.DecimalPlaces = 8;
            this.POS_Y.Increment = new decimal(new int[] {
            1,
            0,
            0,
            851968});
            this.POS_Y.Location = new System.Drawing.Point(180, 195);
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
            this.POS_Y.TabIndex = 148;
            this.POS_Y.Value = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.POS_Y.ValueChanged += new System.EventHandler(this.POS_Y_ValueChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(58, 198);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(17, 13);
            this.label4.TabIndex = 147;
            this.label4.Text = "X:";
            // 
            // POS_X
            // 
            this.POS_X.DecimalPlaces = 8;
            this.POS_X.Increment = new decimal(new int[] {
            1,
            0,
            0,
            851968});
            this.POS_X.Location = new System.Drawing.Point(77, 195);
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
            this.POS_X.TabIndex = 146;
            this.POS_X.Value = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.POS_X.ValueChanged += new System.EventHandler(this.POS_X_ValueChanged);
            // 
            // materials
            // 
            this.materials.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.materials.FormattingEnabled = true;
            this.materials.HorizontalScrollbar = true;
            this.materials.Location = new System.Drawing.Point(68, 49);
            this.materials.Name = "materials";
            this.materials.Size = new System.Drawing.Size(639, 121);
            this.materials.TabIndex = 145;
            // 
            // editMaterial
            // 
            this.editMaterial.Location = new System.Drawing.Point(713, 48);
            this.editMaterial.Name = "editMaterial";
            this.editMaterial.Size = new System.Drawing.Size(108, 122);
            this.editMaterial.TabIndex = 9;
            this.editMaterial.Text = "Change Selected Material";
            this.editMaterial.UseVisualStyleBackColor = true;
            this.editMaterial.Click += new System.EventHandler(this.editMaterial_Click);
            // 
            // editModel
            // 
            this.editModel.Location = new System.Drawing.Point(713, 19);
            this.editModel.Name = "editModel";
            this.editModel.Size = new System.Drawing.Size(108, 23);
            this.editModel.TabIndex = 8;
            this.editModel.Text = "Change Model";
            this.editModel.UseVisualStyleBackColor = true;
            this.editModel.Click += new System.EventHandler(this.editModel_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 52);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(49, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "Materials";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(26, 25);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(36, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Model";
            // 
            // modelInfoTextbox
            // 
            this.modelInfoTextbox.Location = new System.Drawing.Point(68, 21);
            this.modelInfoTextbox.Name = "modelInfoTextbox";
            this.modelInfoTextbox.ReadOnly = true;
            this.modelInfoTextbox.Size = new System.Drawing.Size(639, 20);
            this.modelInfoTextbox.TabIndex = 0;
            // 
            // GUI_Resource_RenderableInstance
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBox1);
            this.Name = "GUI_Resource_RenderableInstance";
            this.Size = new System.Drawing.Size(838, 232);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ROT_Z)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ROT_Y)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ROT_X)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_Z)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_Y)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_X)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox modelInfoTextbox;
        private System.Windows.Forms.Button editMaterial;
        private System.Windows.Forms.Button editModel;
        private System.Windows.Forms.ListBox materials;
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
        private System.Windows.Forms.Label label4;
        private SmoothNumericUpDown POS_X;
    }
}

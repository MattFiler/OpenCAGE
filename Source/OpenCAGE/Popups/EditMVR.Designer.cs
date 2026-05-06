namespace OpenCAGE
{
    partial class EditMVR
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditMVR));
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.renderable = new Popups.UserControls.GUI_Resource_RenderableInstance();
            this.label8 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.SCALE_Z = new SmoothNumericUpDown();
            this.label11 = new System.Windows.Forms.Label();
            this.SCALE_Y = new SmoothNumericUpDown();
            this.label12 = new System.Windows.Forms.Label();
            this.SCALE_X = new SmoothNumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.ROT_Z = new SmoothNumericUpDown();
            this.label5 = new System.Windows.Forms.Label();
            this.ROT_Y = new SmoothNumericUpDown();
            this.label6 = new System.Windows.Forms.Label();
            this.ROT_X = new SmoothNumericUpDown();
            this.label7 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.POS_Z = new SmoothNumericUpDown();
            this.label13 = new System.Windows.Forms.Label();
            this.POS_Y = new SmoothNumericUpDown();
            this.label14 = new System.Windows.Forms.Label();
            this.POS_X = new SmoothNumericUpDown();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.doSave = new System.Windows.Forms.Button();
            this.groupBox7 = new System.Windows.Forms.GroupBox();
            this.emRadiosityMultiplier = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.emIntensityMultiplier = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            this.emTint = new System.Windows.Forms.PictureBox();
            this.openColourPicker = new System.Windows.Forms.Button();
            this.groupBox6 = new System.Windows.Forms.GroupBox();
            this.isStationary = new System.Windows.Forms.CheckBox();
            this.isVisible = new System.Windows.Forms.CheckBox();
            this.requiresScript = new System.Windows.Forms.CheckBox();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.cfNoTorch = new System.Windows.Forms.CheckBox();
            this.cfNoSize = new System.Windows.Forms.CheckBox();
            this.cfAlwaysPass = new System.Windows.Forms.CheckBox();
            this.cfReflections = new System.Windows.Forms.CheckBox();
            this.cfDontRender = new System.Windows.Forms.CheckBox();
            this.cfDontShadows = new System.Windows.Forms.CheckBox();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.emMasterOff = new System.Windows.Forms.CheckBox();
            this.emReplaceTint = new System.Windows.Forms.CheckBox();
            this.emReplaceIntensity = new System.Windows.Forms.CheckBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.SCALE_Z)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.SCALE_Y)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.SCALE_X)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ROT_Z)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ROT_Y)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ROT_X)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_Z)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_Y)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_X)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox7.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.emRadiosityMultiplier)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.emIntensityMultiplier)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.emTint)).BeginInit();
            this.groupBox6.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // listBox1
            // 
            this.listBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBox1.FormattingEnabled = true;
            this.listBox1.HorizontalScrollbar = true;
            this.listBox1.Location = new System.Drawing.Point(6, 19);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(837, 173);
            this.listBox1.TabIndex = 0;
            this.listBox1.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged);
            // 
            // renderable
            // 
            this.renderable.Location = new System.Drawing.Point(5, 19);
            this.renderable.Name = "renderable";
            this.renderable.Size = new System.Drawing.Size(838, 186);
            this.renderable.TabIndex = 2;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(6, 98);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(34, 13);
            this.label8.TabIndex = 28;
            this.label8.Text = "Scale";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(212, 117);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(17, 13);
            this.label10.TabIndex = 27;
            this.label10.Text = "Z:";
            // 
            // SCALE_Z
            // 
            this.SCALE_Z.DecimalPlaces = 7;
            this.SCALE_Z.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.SCALE_Z.Location = new System.Drawing.Point(232, 114);
            this.SCALE_Z.Maximum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            0});
            this.SCALE_Z.Minimum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.SCALE_Z.Name = "SCALE_Z";
            this.SCALE_Z.Size = new System.Drawing.Size(80, 20);
            this.SCALE_Z.TabIndex = 26;
            this.SCALE_Z.Value = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(109, 117);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(17, 13);
            this.label11.TabIndex = 25;
            this.label11.Text = "Y:";
            // 
            // SCALE_Y
            // 
            this.SCALE_Y.DecimalPlaces = 7;
            this.SCALE_Y.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.SCALE_Y.Location = new System.Drawing.Point(128, 114);
            this.SCALE_Y.Maximum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            0});
            this.SCALE_Y.Minimum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.SCALE_Y.Name = "SCALE_Y";
            this.SCALE_Y.Size = new System.Drawing.Size(80, 20);
            this.SCALE_Y.TabIndex = 24;
            this.SCALE_Y.Value = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(6, 117);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(17, 13);
            this.label12.TabIndex = 23;
            this.label12.Text = "X:";
            // 
            // SCALE_X
            // 
            this.SCALE_X.DecimalPlaces = 7;
            this.SCALE_X.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.SCALE_X.Location = new System.Drawing.Point(25, 114);
            this.SCALE_X.Maximum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            0});
            this.SCALE_X.Minimum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.SCALE_X.Name = "SCALE_X";
            this.SCALE_X.Size = new System.Drawing.Size(80, 20);
            this.SCALE_X.TabIndex = 22;
            this.SCALE_X.Value = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 60);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(47, 13);
            this.label3.TabIndex = 42;
            this.label3.Text = "Rotation";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(212, 79);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(17, 13);
            this.label4.TabIndex = 41;
            this.label4.Text = "Z:";
            // 
            // ROT_Z
            // 
            this.ROT_Z.DecimalPlaces = 7;
            this.ROT_Z.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.ROT_Z.Location = new System.Drawing.Point(232, 76);
            this.ROT_Z.Maximum = new decimal(new int[] {
            360,
            0,
            0,
            0});
            this.ROT_Z.Minimum = new decimal(new int[] {
            360,
            0,
            0,
            -2147483648});
            this.ROT_Z.Name = "ROT_Z";
            this.ROT_Z.Size = new System.Drawing.Size(80, 20);
            this.ROT_Z.TabIndex = 40;
            this.ROT_Z.Value = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(109, 79);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(17, 13);
            this.label5.TabIndex = 39;
            this.label5.Text = "Y:";
            // 
            // ROT_Y
            // 
            this.ROT_Y.DecimalPlaces = 7;
            this.ROT_Y.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.ROT_Y.Location = new System.Drawing.Point(128, 76);
            this.ROT_Y.Maximum = new decimal(new int[] {
            360,
            0,
            0,
            0});
            this.ROT_Y.Minimum = new decimal(new int[] {
            360,
            0,
            0,
            -2147483648});
            this.ROT_Y.Name = "ROT_Y";
            this.ROT_Y.Size = new System.Drawing.Size(80, 20);
            this.ROT_Y.TabIndex = 38;
            this.ROT_Y.Value = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(6, 79);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(17, 13);
            this.label6.TabIndex = 37;
            this.label6.Text = "X:";
            // 
            // ROT_X
            // 
            this.ROT_X.DecimalPlaces = 7;
            this.ROT_X.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.ROT_X.Location = new System.Drawing.Point(25, 76);
            this.ROT_X.Maximum = new decimal(new int[] {
            360,
            0,
            0,
            0});
            this.ROT_X.Minimum = new decimal(new int[] {
            360,
            0,
            0,
            -2147483648});
            this.ROT_X.Name = "ROT_X";
            this.ROT_X.Size = new System.Drawing.Size(80, 20);
            this.ROT_X.TabIndex = 36;
            this.ROT_X.Value = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(6, 21);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(44, 13);
            this.label7.TabIndex = 35;
            this.label7.Text = "Position";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(212, 40);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(17, 13);
            this.label9.TabIndex = 34;
            this.label9.Text = "Z:";
            // 
            // POS_Z
            // 
            this.POS_Z.DecimalPlaces = 7;
            this.POS_Z.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.POS_Z.Location = new System.Drawing.Point(232, 37);
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
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(109, 40);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(17, 13);
            this.label13.TabIndex = 32;
            this.label13.Text = "Y:";
            // 
            // POS_Y
            // 
            this.POS_Y.DecimalPlaces = 7;
            this.POS_Y.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.POS_Y.Location = new System.Drawing.Point(128, 37);
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
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(6, 40);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(17, 13);
            this.label14.TabIndex = 30;
            this.label14.Text = "X:";
            // 
            // POS_X
            // 
            this.POS_X.DecimalPlaces = 7;
            this.POS_X.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.POS_X.Location = new System.Drawing.Point(25, 37);
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
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.listBox1);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(849, 199);
            this.groupBox1.TabIndex = 176;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Movers";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.doSave);
            this.groupBox2.Controls.Add(this.groupBox7);
            this.groupBox2.Controls.Add(this.groupBox6);
            this.groupBox2.Controls.Add(this.groupBox5);
            this.groupBox2.Controls.Add(this.groupBox4);
            this.groupBox2.Controls.Add(this.groupBox3);
            this.groupBox2.Controls.Add(this.renderable);
            this.groupBox2.Location = new System.Drawing.Point(12, 217);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(849, 372);
            this.groupBox2.TabIndex = 177;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Selected Mover Descriptor";
            // 
            // doSave
            // 
            this.doSave.Location = new System.Drawing.Point(757, 307);
            this.doSave.Name = "doSave";
            this.doSave.Size = new System.Drawing.Size(86, 53);
            this.doSave.TabIndex = 197;
            this.doSave.Text = "Save";
            this.doSave.UseVisualStyleBackColor = true;
            this.doSave.Click += new System.EventHandler(this.doSave_Click);
            // 
            // groupBox7
            // 
            this.groupBox7.Controls.Add(this.emRadiosityMultiplier);
            this.groupBox7.Controls.Add(this.label2);
            this.groupBox7.Controls.Add(this.emIntensityMultiplier);
            this.groupBox7.Controls.Add(this.label1);
            this.groupBox7.Controls.Add(this.emTint);
            this.groupBox7.Controls.Add(this.openColourPicker);
            this.groupBox7.Location = new System.Drawing.Point(332, 303);
            this.groupBox7.Name = "groupBox7";
            this.groupBox7.Size = new System.Drawing.Size(419, 56);
            this.groupBox7.TabIndex = 196;
            this.groupBox7.TabStop = false;
            this.groupBox7.Text = "Emissive Properties";
            // 
            // emRadiosityMultiplier
            // 
            this.emRadiosityMultiplier.DecimalPlaces = 5;
            this.emRadiosityMultiplier.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.emRadiosityMultiplier.Location = new System.Drawing.Point(287, 28);
            this.emRadiosityMultiplier.Name = "emRadiosityMultiplier";
            this.emRadiosityMultiplier.Size = new System.Drawing.Size(120, 20);
            this.emRadiosityMultiplier.TabIndex = 199;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(284, 12);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(94, 13);
            this.label2.TabIndex = 198;
            this.label2.Text = "Radiosity Multiplier";
            // 
            // emIntensityMultiplier
            // 
            this.emIntensityMultiplier.DecimalPlaces = 5;
            this.emIntensityMultiplier.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.emIntensityMultiplier.Location = new System.Drawing.Point(140, 28);
            this.emIntensityMultiplier.Name = "emIntensityMultiplier";
            this.emIntensityMultiplier.Size = new System.Drawing.Size(120, 20);
            this.emIntensityMultiplier.TabIndex = 197;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(137, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(90, 13);
            this.label1.TabIndex = 196;
            this.label1.Text = "Intensity Multiplier";
            // 
            // emTint
            // 
            this.emTint.Location = new System.Drawing.Point(7, 16);
            this.emTint.Name = "emTint";
            this.emTint.Size = new System.Drawing.Size(35, 35);
            this.emTint.TabIndex = 195;
            this.emTint.TabStop = false;
            // 
            // openColourPicker
            // 
            this.openColourPicker.Location = new System.Drawing.Point(48, 21);
            this.openColourPicker.Name = "openColourPicker";
            this.openColourPicker.Size = new System.Drawing.Size(71, 23);
            this.openColourPicker.TabIndex = 194;
            this.openColourPicker.Text = "Edit Tint";
            this.openColourPicker.UseVisualStyleBackColor = true;
            this.openColourPicker.Click += new System.EventHandler(this.openColourPicker_Click);
            // 
            // groupBox6
            // 
            this.groupBox6.Controls.Add(this.isStationary);
            this.groupBox6.Controls.Add(this.isVisible);
            this.groupBox6.Controls.Add(this.requiresScript);
            this.groupBox6.Location = new System.Drawing.Point(732, 211);
            this.groupBox6.Name = "groupBox6";
            this.groupBox6.Size = new System.Drawing.Size(111, 92);
            this.groupBox6.TabIndex = 193;
            this.groupBox6.TabStop = false;
            this.groupBox6.Text = "Instance Flags";
            // 
            // isStationary
            // 
            this.isStationary.AutoSize = true;
            this.isStationary.Location = new System.Drawing.Point(6, 65);
            this.isStationary.Name = "isStationary";
            this.isStationary.Size = new System.Drawing.Size(73, 17);
            this.isStationary.TabIndex = 180;
            this.isStationary.Text = "Stationary";
            this.isStationary.UseVisualStyleBackColor = true;
            // 
            // isVisible
            // 
            this.isVisible.AutoSize = true;
            this.isVisible.Location = new System.Drawing.Point(6, 42);
            this.isVisible.Name = "isVisible";
            this.isVisible.Size = new System.Drawing.Size(98, 17);
            this.isVisible.TabIndex = 179;
            this.isVisible.Text = "Visible On Start";
            this.isVisible.UseVisualStyleBackColor = true;
            // 
            // requiresScript
            // 
            this.requiresScript.AutoSize = true;
            this.requiresScript.Location = new System.Drawing.Point(6, 19);
            this.requiresScript.Name = "requiresScript";
            this.requiresScript.Size = new System.Drawing.Size(98, 17);
            this.requiresScript.TabIndex = 178;
            this.requiresScript.Text = "Requires Script";
            this.requiresScript.UseVisualStyleBackColor = true;
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.cfNoTorch);
            this.groupBox5.Controls.Add(this.cfNoSize);
            this.groupBox5.Controls.Add(this.cfAlwaysPass);
            this.groupBox5.Controls.Add(this.cfReflections);
            this.groupBox5.Controls.Add(this.cfDontRender);
            this.groupBox5.Controls.Add(this.cfDontShadows);
            this.groupBox5.Location = new System.Drawing.Point(455, 211);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(271, 92);
            this.groupBox5.TabIndex = 192;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Cull Flags";
            // 
            // cfNoTorch
            // 
            this.cfNoTorch.AutoSize = true;
            this.cfNoTorch.Location = new System.Drawing.Point(147, 42);
            this.cfNoTorch.Name = "cfNoTorch";
            this.cfNoTorch.Size = new System.Drawing.Size(113, 17);
            this.cfNoTorch.TabIndex = 189;
            this.cfNoTorch.Text = "No Torch Shadow";
            this.toolTip1.SetToolTip(this.cfNoTorch, "If set, this instance will not cast shadows in the player\'s torch beam.");
            this.cfNoTorch.UseVisualStyleBackColor = true;
            // 
            // cfNoSize
            // 
            this.cfNoSize.AutoSize = true;
            this.cfNoSize.Location = new System.Drawing.Point(147, 19);
            this.cfNoSize.Name = "cfNoSize";
            this.cfNoSize.Size = new System.Drawing.Size(97, 17);
            this.cfNoSize.TabIndex = 188;
            this.cfNoSize.Text = "No Size Culling";
            this.toolTip1.SetToolTip(this.cfNoSize, "If set, this instance will not be culled due to its size.");
            this.cfNoSize.UseVisualStyleBackColor = true;
            // 
            // cfAlwaysPass
            // 
            this.cfAlwaysPass.AutoSize = true;
            this.cfAlwaysPass.Location = new System.Drawing.Point(147, 65);
            this.cfAlwaysPass.Name = "cfAlwaysPass";
            this.cfAlwaysPass.Size = new System.Drawing.Size(75, 17);
            this.cfAlwaysPass.TabIndex = 187;
            this.cfAlwaysPass.Text = "Never Cull";
            this.toolTip1.SetToolTip(this.cfAlwaysPass, "If set, this instance will never be culled.");
            this.cfAlwaysPass.UseVisualStyleBackColor = true;
            // 
            // cfReflections
            // 
            this.cfReflections.AutoSize = true;
            this.cfReflections.Location = new System.Drawing.Point(8, 65);
            this.cfReflections.Name = "cfReflections";
            this.cfReflections.Size = new System.Drawing.Size(129, 17);
            this.cfReflections.TabIndex = 186;
            this.cfReflections.Text = "Include In Reflections";
            this.toolTip1.SetToolTip(this.cfReflections, "If set, this instance will be included in planar reflections.");
            this.cfReflections.UseVisualStyleBackColor = true;
            // 
            // cfDontRender
            // 
            this.cfDontRender.AutoSize = true;
            this.cfDontRender.Location = new System.Drawing.Point(8, 42);
            this.cfDontRender.Name = "cfDontRender";
            this.cfDontRender.Size = new System.Drawing.Size(89, 17);
            this.cfDontRender.TabIndex = 185;
            this.cfDontRender.Text = "Don\'t Render";
            this.toolTip1.SetToolTip(this.cfDontRender, "If set, this instance will not render.");
            this.cfDontRender.UseVisualStyleBackColor = true;
            // 
            // cfDontShadows
            // 
            this.cfDontShadows.AutoSize = true;
            this.cfDontShadows.Location = new System.Drawing.Point(8, 19);
            this.cfDontShadows.Name = "cfDontShadows";
            this.cfDontShadows.Size = new System.Drawing.Size(122, 17);
            this.cfDontShadows.TabIndex = 184;
            this.cfDontShadows.Text = "Don\'t Cast Shadows";
            this.toolTip1.SetToolTip(this.cfDontShadows, "If set, this instance will not cast shadows.");
            this.cfDontShadows.UseVisualStyleBackColor = true;
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.SCALE_X);
            this.groupBox4.Controls.Add(this.label12);
            this.groupBox4.Controls.Add(this.SCALE_Y);
            this.groupBox4.Controls.Add(this.label11);
            this.groupBox4.Controls.Add(this.label3);
            this.groupBox4.Controls.Add(this.SCALE_Z);
            this.groupBox4.Controls.Add(this.label4);
            this.groupBox4.Controls.Add(this.label10);
            this.groupBox4.Controls.Add(this.ROT_Z);
            this.groupBox4.Controls.Add(this.label8);
            this.groupBox4.Controls.Add(this.label5);
            this.groupBox4.Controls.Add(this.POS_X);
            this.groupBox4.Controls.Add(this.ROT_Y);
            this.groupBox4.Controls.Add(this.label14);
            this.groupBox4.Controls.Add(this.label6);
            this.groupBox4.Controls.Add(this.POS_Y);
            this.groupBox4.Controls.Add(this.ROT_X);
            this.groupBox4.Controls.Add(this.label13);
            this.groupBox4.Controls.Add(this.label7);
            this.groupBox4.Controls.Add(this.POS_Z);
            this.groupBox4.Controls.Add(this.label9);
            this.groupBox4.Location = new System.Drawing.Point(9, 211);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(318, 149);
            this.groupBox4.TabIndex = 191;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Global Transform";
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.emMasterOff);
            this.groupBox3.Controls.Add(this.emReplaceTint);
            this.groupBox3.Controls.Add(this.emReplaceIntensity);
            this.groupBox3.Location = new System.Drawing.Point(333, 211);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(116, 91);
            this.groupBox3.TabIndex = 190;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Emissive Flags";
            // 
            // emMasterOff
            // 
            this.emMasterOff.AutoSize = true;
            this.emMasterOff.Location = new System.Drawing.Point(6, 65);
            this.emMasterOff.Name = "emMasterOff";
            this.emMasterOff.Size = new System.Drawing.Size(105, 17);
            this.emMasterOff.TabIndex = 183;
            this.emMasterOff.Text = "Disable Emissive";
            this.emMasterOff.UseVisualStyleBackColor = true;
            // 
            // emReplaceTint
            // 
            this.emReplaceTint.AutoSize = true;
            this.emReplaceTint.Location = new System.Drawing.Point(6, 19);
            this.emReplaceTint.Name = "emReplaceTint";
            this.emReplaceTint.Size = new System.Drawing.Size(87, 17);
            this.emReplaceTint.TabIndex = 181;
            this.emReplaceTint.Text = "Replace Tint";
            this.emReplaceTint.UseVisualStyleBackColor = true;
            // 
            // emReplaceIntensity
            // 
            this.emReplaceIntensity.AutoSize = true;
            this.emReplaceIntensity.Location = new System.Drawing.Point(6, 42);
            this.emReplaceIntensity.Name = "emReplaceIntensity";
            this.emReplaceIntensity.Size = new System.Drawing.Size(108, 17);
            this.emReplaceIntensity.TabIndex = 182;
            this.emReplaceIntensity.Text = "Replace Intensity";
            this.emReplaceIntensity.UseVisualStyleBackColor = true;
            // 
            // EditMVR
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(871, 600);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "EditMVR";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Edit Mover Descriptors";
            ((System.ComponentModel.ISupportInitialize)(this.SCALE_Z)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.SCALE_Y)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.SCALE_X)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ROT_Z)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ROT_Y)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ROT_X)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_Z)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_Y)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_X)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox7.ResumeLayout(false);
            this.groupBox7.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.emRadiosityMultiplier)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.emIntensityMultiplier)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.emTint)).EndInit();
            this.groupBox6.ResumeLayout(false);
            this.groupBox6.PerformLayout();
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox listBox1;
        private Popups.UserControls.GUI_Resource_RenderableInstance renderable;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label10;
        private SmoothNumericUpDown SCALE_Z;
        private System.Windows.Forms.Label label11;
        private SmoothNumericUpDown SCALE_Y;
        private System.Windows.Forms.Label label12;
        private SmoothNumericUpDown SCALE_X;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private SmoothNumericUpDown ROT_Z;
        private System.Windows.Forms.Label label5;
        private SmoothNumericUpDown ROT_Y;
        private System.Windows.Forms.Label label6;
        private SmoothNumericUpDown ROT_X;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label9;
        private SmoothNumericUpDown POS_Z;
        private System.Windows.Forms.Label label13;
        private SmoothNumericUpDown POS_Y;
        private System.Windows.Forms.Label label14;
        private SmoothNumericUpDown POS_X;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.CheckBox isStationary;
        private System.Windows.Forms.CheckBox isVisible;
        private System.Windows.Forms.CheckBox requiresScript;
        private System.Windows.Forms.CheckBox cfAlwaysPass;
        private System.Windows.Forms.CheckBox cfReflections;
        private System.Windows.Forms.CheckBox cfDontRender;
        private System.Windows.Forms.CheckBox cfDontShadows;
        private System.Windows.Forms.CheckBox emMasterOff;
        private System.Windows.Forms.CheckBox emReplaceIntensity;
        private System.Windows.Forms.CheckBox emReplaceTint;
        private System.Windows.Forms.CheckBox cfNoTorch;
        private System.Windows.Forms.CheckBox cfNoSize;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.GroupBox groupBox6;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.GroupBox groupBox7;
        private System.Windows.Forms.NumericUpDown emRadiosityMultiplier;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown emIntensityMultiplier;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.PictureBox emTint;
        private System.Windows.Forms.Button openColourPicker;
        private System.Windows.Forms.Button doSave;
    }
}

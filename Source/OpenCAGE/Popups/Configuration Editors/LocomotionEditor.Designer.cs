namespace CommandsEditor.ConfigEditors
{
    partial class LocomotionEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LocomotionEditor));
            this.characters = new System.Windows.Forms.ComboBox();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.permittedLocomotionModulation = new System.Windows.Forms.NumericUpDown();
            this.capsuleHeight = new System.Windows.Forms.NumericUpDown();
            this.capsuleRadius = new System.Windows.Forms.NumericUpDown();
            this.label34 = new System.Windows.Forms.Label();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.steeringBoundarySet1 = new SteeringBoundarySet();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.steeringBoundarySet2 = new SteeringBoundarySet();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.steeringBoundarySet3 = new SteeringBoundarySet();
            this.helpBtn = new System.Windows.Forms.Button();
            this.groupBox4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.permittedLocomotionModulation)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.capsuleHeight)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.capsuleRadius)).BeginInit();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.SuspendLayout();
            // 
            // characters
            // 
            this.characters.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.characters.FormattingEnabled = true;
            this.characters.Location = new System.Drawing.Point(12, 12);
            this.characters.Name = "characters";
            this.characters.Size = new System.Drawing.Size(434, 21);
            this.characters.TabIndex = 523;
            this.characters.SelectedIndexChanged += new System.EventHandler(this.characters_SelectedIndexChanged);
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.label2);
            this.groupBox4.Controls.Add(this.label1);
            this.groupBox4.Controls.Add(this.permittedLocomotionModulation);
            this.groupBox4.Controls.Add(this.capsuleHeight);
            this.groupBox4.Controls.Add(this.capsuleRadius);
            this.groupBox4.Controls.Add(this.label34);
            this.groupBox4.Location = new System.Drawing.Point(12, 39);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(434, 111);
            this.groupBox4.TabIndex = 528;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Misc Locomotion Settings";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(223, 21);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(79, 13);
            this.label2.TabIndex = 519;
            this.label2.Text = "Capsule Height";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 62);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(117, 13);
            this.label1.TabIndex = 518;
            this.label1.Text = "Locomotion Modulation";
            // 
            // permittedLocomotionModulation
            // 
            this.permittedLocomotionModulation.DecimalPlaces = 3;
            this.permittedLocomotionModulation.Location = new System.Drawing.Point(16, 78);
            this.permittedLocomotionModulation.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.permittedLocomotionModulation.Name = "permittedLocomotionModulation";
            this.permittedLocomotionModulation.Size = new System.Drawing.Size(187, 20);
            this.permittedLocomotionModulation.TabIndex = 517;
            // 
            // capsuleHeight
            // 
            this.capsuleHeight.DecimalPlaces = 3;
            this.capsuleHeight.Location = new System.Drawing.Point(226, 37);
            this.capsuleHeight.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.capsuleHeight.Name = "capsuleHeight";
            this.capsuleHeight.Size = new System.Drawing.Size(187, 20);
            this.capsuleHeight.TabIndex = 516;
            // 
            // capsuleRadius
            // 
            this.capsuleRadius.DecimalPlaces = 3;
            this.capsuleRadius.Location = new System.Drawing.Point(16, 37);
            this.capsuleRadius.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.capsuleRadius.Name = "capsuleRadius";
            this.capsuleRadius.Size = new System.Drawing.Size(187, 20);
            this.capsuleRadius.TabIndex = 515;
            // 
            // label34
            // 
            this.label34.AutoSize = true;
            this.label34.Location = new System.Drawing.Point(13, 21);
            this.label34.Name = "label34";
            this.label34.Size = new System.Drawing.Size(81, 13);
            this.label34.TabIndex = 513;
            this.label34.Text = "Capsule Radius";
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Location = new System.Drawing.Point(11, 157);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(438, 279);
            this.tabControl1.TabIndex = 532;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.steeringBoundarySet1);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(430, 253);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Default Steering";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // steeringBoundarySet1
            // 
            this.steeringBoundarySet1.Location = new System.Drawing.Point(6, 5);
            this.steeringBoundarySet1.Name = "steeringBoundarySet1";
            this.steeringBoundarySet1.Size = new System.Drawing.Size(419, 242);
            this.steeringBoundarySet1.TabIndex = 532;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.steeringBoundarySet2);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(430, 253);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Aimed Steering";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // steeringBoundarySet2
            // 
            this.steeringBoundarySet2.Location = new System.Drawing.Point(6, 5);
            this.steeringBoundarySet2.Name = "steeringBoundarySet2";
            this.steeringBoundarySet2.Size = new System.Drawing.Size(419, 242);
            this.steeringBoundarySet2.TabIndex = 532;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.steeringBoundarySet3);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Size = new System.Drawing.Size(430, 253);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Crouched Steering";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // steeringBoundarySet3
            // 
            this.steeringBoundarySet3.Location = new System.Drawing.Point(6, 5);
            this.steeringBoundarySet3.Name = "steeringBoundarySet3";
            this.steeringBoundarySet3.Size = new System.Drawing.Size(419, 242);
            this.steeringBoundarySet3.TabIndex = 532;
            // 
            // helpBtn
            // 
            this.helpBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.helpBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.helpBtn.Image = ((System.Drawing.Image)(resources.GetObject("helpBtn.Image")));
            this.helpBtn.Location = new System.Drawing.Point(440, 0);
            this.helpBtn.Name = "helpBtn";
            this.helpBtn.Size = new System.Drawing.Size(20, 20);
            this.helpBtn.TabIndex = 533;
            this.helpBtn.UseVisualStyleBackColor = true;
            this.helpBtn.Click += new System.EventHandler(this.helpBtn_Click);
            // 
            // LocomotionEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(460, 443);
            this.Controls.Add(this.helpBtn);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.characters);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "LocomotionEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Locomotion Editor";
            this.Load += new System.EventHandler(this.LocomotionEditor_Load);
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.permittedLocomotionModulation)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.capsuleHeight)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.capsuleRadius)).EndInit();
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.ComboBox characters;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Label label34;
        private System.Windows.Forms.NumericUpDown capsuleRadius;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private SteeringBoundarySet steeringBoundarySet1;
        private System.Windows.Forms.TabPage tabPage2;
        private SteeringBoundarySet steeringBoundarySet2;
        private System.Windows.Forms.TabPage tabPage3;
        private SteeringBoundarySet steeringBoundarySet3;
        private System.Windows.Forms.Button helpBtn;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown permittedLocomotionModulation;
        private System.Windows.Forms.NumericUpDown capsuleHeight;
    }
}

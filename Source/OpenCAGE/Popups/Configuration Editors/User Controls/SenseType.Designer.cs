namespace OpenCAGE.ConfigEditors
{
    partial class SenseType
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
            this.label8 = new System.Windows.Forms.Label();
            this.positional_accuracy_scalar = new System.Windows.Forms.NumericUpDown();
            this.label7 = new System.Windows.Forms.Label();
            this.last_sensed_expire_time = new System.Windows.Forms.NumericUpDown();
            this.label6 = new System.Windows.Forms.Label();
            this.min_activated_time = new System.Windows.Forms.NumericUpDown();
            this.label5 = new System.Windows.Forms.Label();
            this.decay_per_second = new System.Windows.Forms.NumericUpDown();
            this.label4 = new System.Windows.Forms.Label();
            this.trace_threshold = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.activation_threshold = new System.Windows.Forms.NumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.lower_threshold = new System.Windows.Forms.NumericUpDown();
            this.upper_threshold = new System.Windows.Forms.NumericUpDown();
            this.label9 = new System.Windows.Forms.Label();
            this.label21 = new System.Windows.Forms.Label();
            this.combined_sense_activation_scalar = new System.Windows.Forms.NumericUpDown();
            this.label22 = new System.Windows.Forms.Label();
            this.combined_sense_min_raw_activation = new System.Windows.Forms.NumericUpDown();
            this.combined_sense_max_raw_activation = new System.Windows.Forms.NumericUpDown();
            this.label26 = new System.Windows.Forms.Label();
            this.min_raw_activation = new System.Windows.Forms.NumericUpDown();
            this.label11 = new System.Windows.Forms.Label();
            this.max_raw_activation = new System.Windows.Forms.NumericUpDown();
            this.label18 = new System.Windows.Forms.Label();
            this.activation_scalar = new System.Windows.Forms.NumericUpDown();
            this.label29 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.positional_accuracy_scalar)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.last_sensed_expire_time)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.min_activated_time)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.decay_per_second)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trace_threshold)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.activation_threshold)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.lower_threshold)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.upper_threshold)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.combined_sense_activation_scalar)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.combined_sense_min_raw_activation)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.combined_sense_max_raw_activation)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.min_raw_activation)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.max_raw_activation)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.activation_scalar)).BeginInit();
            this.SuspendLayout();
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(3, 198);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(133, 13);
            this.label8.TabIndex = 597;
            this.label8.Text = "Positional Accuracy Scalar";
            // 
            // positional_accuracy_scalar
            // 
            this.positional_accuracy_scalar.DecimalPlaces = 3;
            this.positional_accuracy_scalar.Location = new System.Drawing.Point(5, 214);
            this.positional_accuracy_scalar.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.positional_accuracy_scalar.Name = "positional_accuracy_scalar";
            this.positional_accuracy_scalar.Size = new System.Drawing.Size(187, 20);
            this.positional_accuracy_scalar.TabIndex = 598;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(220, 159);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(124, 13);
            this.label7.TabIndex = 595;
            this.label7.Text = "Last Sensed Expire Time";
            // 
            // last_sensed_expire_time
            // 
            this.last_sensed_expire_time.DecimalPlaces = 3;
            this.last_sensed_expire_time.Location = new System.Drawing.Point(223, 175);
            this.last_sensed_expire_time.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.last_sensed_expire_time.Name = "last_sensed_expire_time";
            this.last_sensed_expire_time.Size = new System.Drawing.Size(187, 20);
            this.last_sensed_expire_time.TabIndex = 596;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(3, 159);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(98, 13);
            this.label6.TabIndex = 593;
            this.label6.Text = "Min Activated Time";
            // 
            // min_activated_time
            // 
            this.min_activated_time.DecimalPlaces = 3;
            this.min_activated_time.Location = new System.Drawing.Point(6, 175);
            this.min_activated_time.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.min_activated_time.Name = "min_activated_time";
            this.min_activated_time.Size = new System.Drawing.Size(187, 20);
            this.min_activated_time.TabIndex = 594;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(3, 120);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(97, 13);
            this.label5.TabIndex = 591;
            this.label5.Text = "Decay Per Second";
            // 
            // decay_per_second
            // 
            this.decay_per_second.DecimalPlaces = 3;
            this.decay_per_second.Location = new System.Drawing.Point(6, 136);
            this.decay_per_second.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.decay_per_second.Name = "decay_per_second";
            this.decay_per_second.Size = new System.Drawing.Size(187, 20);
            this.decay_per_second.TabIndex = 592;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(220, 120);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(85, 13);
            this.label4.TabIndex = 589;
            this.label4.Text = "Trace Threshold";
            // 
            // trace_threshold
            // 
            this.trace_threshold.DecimalPlaces = 3;
            this.trace_threshold.Location = new System.Drawing.Point(223, 136);
            this.trace_threshold.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.trace_threshold.Name = "trace_threshold";
            this.trace_threshold.Size = new System.Drawing.Size(187, 20);
            this.trace_threshold.TabIndex = 590;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(220, 81);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(104, 13);
            this.label2.TabIndex = 587;
            this.label2.Text = "Activation Threshold";
            // 
            // activation_threshold
            // 
            this.activation_threshold.DecimalPlaces = 3;
            this.activation_threshold.Location = new System.Drawing.Point(223, 97);
            this.activation_threshold.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.activation_threshold.Name = "activation_threshold";
            this.activation_threshold.Size = new System.Drawing.Size(187, 20);
            this.activation_threshold.TabIndex = 588;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(3, 81);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(54, 13);
            this.label3.TabIndex = 583;
            this.label3.Text = "Threshold";
            // 
            // lower_threshold
            // 
            this.lower_threshold.DecimalPlaces = 3;
            this.lower_threshold.Location = new System.Drawing.Point(7, 97);
            this.lower_threshold.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.lower_threshold.Name = "lower_threshold";
            this.lower_threshold.Size = new System.Drawing.Size(49, 20);
            this.lower_threshold.TabIndex = 584;
            // 
            // upper_threshold
            // 
            this.upper_threshold.DecimalPlaces = 3;
            this.upper_threshold.Location = new System.Drawing.Point(144, 97);
            this.upper_threshold.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.upper_threshold.Name = "upper_threshold";
            this.upper_threshold.Size = new System.Drawing.Size(49, 20);
            this.upper_threshold.TabIndex = 585;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(59, 100);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(83, 13);
            this.label9.TabIndex = 586;
            this.label9.Text = "Lower --> Upper";
            // 
            // label21
            // 
            this.label21.AutoSize = true;
            this.label21.Location = new System.Drawing.Point(219, 42);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(170, 13);
            this.label21.TabIndex = 581;
            this.label21.Text = "Combined Sense Activation Scalar";
            // 
            // combined_sense_activation_scalar
            // 
            this.combined_sense_activation_scalar.DecimalPlaces = 3;
            this.combined_sense_activation_scalar.Location = new System.Drawing.Point(222, 58);
            this.combined_sense_activation_scalar.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.combined_sense_activation_scalar.Name = "combined_sense_activation_scalar";
            this.combined_sense_activation_scalar.Size = new System.Drawing.Size(187, 20);
            this.combined_sense_activation_scalar.TabIndex = 582;
            // 
            // label22
            // 
            this.label22.AutoSize = true;
            this.label22.Location = new System.Drawing.Point(2, 42);
            this.label22.Name = "label22";
            this.label22.Size = new System.Drawing.Size(162, 13);
            this.label22.TabIndex = 577;
            this.label22.Text = "Combined Sense Raw Activation";
            // 
            // combined_sense_min_raw_activation
            // 
            this.combined_sense_min_raw_activation.DecimalPlaces = 3;
            this.combined_sense_min_raw_activation.Location = new System.Drawing.Point(6, 58);
            this.combined_sense_min_raw_activation.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.combined_sense_min_raw_activation.Name = "combined_sense_min_raw_activation";
            this.combined_sense_min_raw_activation.Size = new System.Drawing.Size(49, 20);
            this.combined_sense_min_raw_activation.TabIndex = 578;
            // 
            // combined_sense_max_raw_activation
            // 
            this.combined_sense_max_raw_activation.DecimalPlaces = 3;
            this.combined_sense_max_raw_activation.Location = new System.Drawing.Point(143, 58);
            this.combined_sense_max_raw_activation.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.combined_sense_max_raw_activation.Name = "combined_sense_max_raw_activation";
            this.combined_sense_max_raw_activation.Size = new System.Drawing.Size(49, 20);
            this.combined_sense_max_raw_activation.TabIndex = 579;
            // 
            // label26
            // 
            this.label26.AutoSize = true;
            this.label26.Location = new System.Drawing.Point(61, 61);
            this.label26.Name = "label26";
            this.label26.Size = new System.Drawing.Size(77, 13);
            this.label26.TabIndex = 580;
            this.label26.Text = "Min -------> Max";
            // 
            // min_raw_activation
            // 
            this.min_raw_activation.DecimalPlaces = 3;
            this.min_raw_activation.Location = new System.Drawing.Point(5, 19);
            this.min_raw_activation.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.min_raw_activation.Name = "min_raw_activation";
            this.min_raw_activation.Size = new System.Drawing.Size(48, 20);
            this.min_raw_activation.TabIndex = 574;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(219, 3);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(87, 13);
            this.label11.TabIndex = 572;
            this.label11.Text = "Activation Scalar";
            // 
            // max_raw_activation
            // 
            this.max_raw_activation.DecimalPlaces = 3;
            this.max_raw_activation.Location = new System.Drawing.Point(142, 19);
            this.max_raw_activation.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.max_raw_activation.Name = "max_raw_activation";
            this.max_raw_activation.Size = new System.Drawing.Size(48, 20);
            this.max_raw_activation.TabIndex = 575;
            // 
            // label18
            // 
            this.label18.AutoSize = true;
            this.label18.Location = new System.Drawing.Point(59, 22);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(77, 13);
            this.label18.TabIndex = 573;
            this.label18.Text = "Min -------> Max";
            // 
            // activation_scalar
            // 
            this.activation_scalar.DecimalPlaces = 3;
            this.activation_scalar.Location = new System.Drawing.Point(222, 19);
            this.activation_scalar.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.activation_scalar.Name = "activation_scalar";
            this.activation_scalar.Size = new System.Drawing.Size(187, 20);
            this.activation_scalar.TabIndex = 576;
            // 
            // label29
            // 
            this.label29.AutoSize = true;
            this.label29.Location = new System.Drawing.Point(3, 3);
            this.label29.Name = "label29";
            this.label29.Size = new System.Drawing.Size(79, 13);
            this.label29.TabIndex = 571;
            this.label29.Text = "Raw Activation";
            // 
            // SenseType
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.label8);
            this.Controls.Add(this.positional_accuracy_scalar);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.last_sensed_expire_time);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.min_activated_time);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.decay_per_second);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.trace_threshold);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.activation_threshold);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.lower_threshold);
            this.Controls.Add(this.upper_threshold);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.label21);
            this.Controls.Add(this.combined_sense_activation_scalar);
            this.Controls.Add(this.label22);
            this.Controls.Add(this.combined_sense_min_raw_activation);
            this.Controls.Add(this.combined_sense_max_raw_activation);
            this.Controls.Add(this.label26);
            this.Controls.Add(this.min_raw_activation);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.max_raw_activation);
            this.Controls.Add(this.label18);
            this.Controls.Add(this.activation_scalar);
            this.Controls.Add(this.label29);
            this.Name = "SenseType";
            this.Size = new System.Drawing.Size(414, 241);
            ((System.ComponentModel.ISupportInitialize)(this.positional_accuracy_scalar)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.last_sensed_expire_time)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.min_activated_time)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.decay_per_second)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trace_threshold)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.activation_threshold)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.lower_threshold)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.upper_threshold)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.combined_sense_activation_scalar)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.combined_sense_min_raw_activation)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.combined_sense_max_raw_activation)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.min_raw_activation)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.max_raw_activation)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.activation_scalar)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.NumericUpDown positional_accuracy_scalar;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.NumericUpDown last_sensed_expire_time;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.NumericUpDown min_activated_time;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.NumericUpDown decay_per_second;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.NumericUpDown trace_threshold;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown activation_threshold;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown lower_threshold;
        private System.Windows.Forms.NumericUpDown upper_threshold;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.NumericUpDown combined_sense_activation_scalar;
        private System.Windows.Forms.Label label22;
        private System.Windows.Forms.NumericUpDown combined_sense_min_raw_activation;
        private System.Windows.Forms.NumericUpDown combined_sense_max_raw_activation;
        private System.Windows.Forms.Label label26;
        private System.Windows.Forms.NumericUpDown min_raw_activation;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.NumericUpDown max_raw_activation;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.NumericUpDown activation_scalar;
        private System.Windows.Forms.Label label29;
    }
}

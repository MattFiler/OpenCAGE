namespace OpenCAGE.ConfigEditors
{ 
    partial class SteeringBoundary
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
            this.label11 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.label16 = new System.Windows.Forms.Label();
            this.label17 = new System.Windows.Forms.Label();
            this.label18 = new System.Windows.Forms.Label();
            this.maxAngularWarping = new System.Windows.Forms.NumericUpDown();
            this.corneringWeight = new System.Windows.Forms.NumericUpDown();
            this.angularAcceleration = new System.Windows.Forms.NumericUpDown();
            this.linearAcceleration = new System.Windows.Forms.NumericUpDown();
            this.linearVelocity = new System.Windows.Forms.NumericUpDown();
            this.maxAngularVelocity = new System.Windows.Forms.NumericUpDown();
            this.corneringPenalty = new System.Windows.Forms.NumericUpDown();
            this.maxLinearWarping = new System.Windows.Forms.NumericUpDown();
            this.stoppingDistance = new System.Windows.Forms.NumericUpDown();
            ((System.ComponentModel.ISupportInitialize)(this.maxAngularWarping)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.corneringWeight)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.angularAcceleration)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.linearAcceleration)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.linearVelocity)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.maxAngularVelocity)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.corneringPenalty)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.maxLinearWarping)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.stoppingDistance)).BeginInit();
            this.SuspendLayout();
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(3, 117);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(102, 13);
            this.label8.TabIndex = 543;
            this.label8.Text = "Max Linear Warping";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(200, 117);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(109, 13);
            this.label11.TabIndex = 541;
            this.label11.Text = "Max Angular Warping";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(3, 78);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(90, 13);
            this.label12.TabIndex = 539;
            this.label12.Text = "Cornering Penalty";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(200, 78);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(89, 13);
            this.label13.TabIndex = 537;
            this.label13.Text = "Cornering Weight";
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(3, 156);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(94, 13);
            this.label14.TabIndex = 535;
            this.label14.Text = "Stopping Distance";
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(200, 39);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(105, 13);
            this.label15.TabIndex = 533;
            this.label15.Text = "Angular Acceleration";
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(3, 39);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(106, 13);
            this.label16.TabIndex = 531;
            this.label16.Text = "Max Angular Velocity";
            // 
            // label17
            // 
            this.label17.AutoSize = true;
            this.label17.Location = new System.Drawing.Point(200, 0);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(98, 13);
            this.label17.TabIndex = 529;
            this.label17.Text = "Linear Acceleration";
            // 
            // label18
            // 
            this.label18.AutoSize = true;
            this.label18.Location = new System.Drawing.Point(3, 0);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(99, 13);
            this.label18.TabIndex = 527;
            this.label18.Text = "Max Linear Velocity";
            // 
            // maxAngularWarping
            // 
            this.maxAngularWarping.DecimalPlaces = 3;
            this.maxAngularWarping.Location = new System.Drawing.Point(203, 134);
            this.maxAngularWarping.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.maxAngularWarping.Name = "maxAngularWarping";
            this.maxAngularWarping.Size = new System.Drawing.Size(187, 20);
            this.maxAngularWarping.TabIndex = 545;
            // 
            // corneringWeight
            // 
            this.corneringWeight.DecimalPlaces = 3;
            this.corneringWeight.Location = new System.Drawing.Point(203, 94);
            this.corneringWeight.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.corneringWeight.Name = "corneringWeight";
            this.corneringWeight.Size = new System.Drawing.Size(187, 20);
            this.corneringWeight.TabIndex = 546;
            // 
            // angularAcceleration
            // 
            this.angularAcceleration.DecimalPlaces = 3;
            this.angularAcceleration.Location = new System.Drawing.Point(203, 55);
            this.angularAcceleration.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.angularAcceleration.Name = "angularAcceleration";
            this.angularAcceleration.Size = new System.Drawing.Size(187, 20);
            this.angularAcceleration.TabIndex = 547;
            // 
            // linearAcceleration
            // 
            this.linearAcceleration.DecimalPlaces = 3;
            this.linearAcceleration.Location = new System.Drawing.Point(203, 16);
            this.linearAcceleration.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.linearAcceleration.Name = "linearAcceleration";
            this.linearAcceleration.Size = new System.Drawing.Size(187, 20);
            this.linearAcceleration.TabIndex = 548;
            // 
            // linearVelocity
            // 
            this.linearVelocity.DecimalPlaces = 3;
            this.linearVelocity.Location = new System.Drawing.Point(6, 16);
            this.linearVelocity.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.linearVelocity.Name = "linearVelocity";
            this.linearVelocity.Size = new System.Drawing.Size(187, 20);
            this.linearVelocity.TabIndex = 549;
            // 
            // maxAngularVelocity
            // 
            this.maxAngularVelocity.DecimalPlaces = 3;
            this.maxAngularVelocity.Location = new System.Drawing.Point(6, 55);
            this.maxAngularVelocity.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.maxAngularVelocity.Name = "maxAngularVelocity";
            this.maxAngularVelocity.Size = new System.Drawing.Size(187, 20);
            this.maxAngularVelocity.TabIndex = 550;
            // 
            // corneringPenalty
            // 
            this.corneringPenalty.DecimalPlaces = 3;
            this.corneringPenalty.Location = new System.Drawing.Point(6, 94);
            this.corneringPenalty.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.corneringPenalty.Name = "corneringPenalty";
            this.corneringPenalty.Size = new System.Drawing.Size(187, 20);
            this.corneringPenalty.TabIndex = 551;
            // 
            // maxLinearWarping
            // 
            this.maxLinearWarping.DecimalPlaces = 3;
            this.maxLinearWarping.Location = new System.Drawing.Point(6, 134);
            this.maxLinearWarping.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.maxLinearWarping.Name = "maxLinearWarping";
            this.maxLinearWarping.Size = new System.Drawing.Size(187, 20);
            this.maxLinearWarping.TabIndex = 552;
            // 
            // stoppingDistance
            // 
            this.stoppingDistance.DecimalPlaces = 3;
            this.stoppingDistance.Location = new System.Drawing.Point(6, 172);
            this.stoppingDistance.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.stoppingDistance.Name = "stoppingDistance";
            this.stoppingDistance.Size = new System.Drawing.Size(187, 20);
            this.stoppingDistance.TabIndex = 553;
            // 
            // SteeringBoundary
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.stoppingDistance);
            this.Controls.Add(this.maxLinearWarping);
            this.Controls.Add(this.corneringPenalty);
            this.Controls.Add(this.maxAngularVelocity);
            this.Controls.Add(this.linearVelocity);
            this.Controls.Add(this.linearAcceleration);
            this.Controls.Add(this.angularAcceleration);
            this.Controls.Add(this.corneringWeight);
            this.Controls.Add(this.maxAngularWarping);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.label12);
            this.Controls.Add(this.label13);
            this.Controls.Add(this.label14);
            this.Controls.Add(this.label15);
            this.Controls.Add(this.label16);
            this.Controls.Add(this.label17);
            this.Controls.Add(this.label18);
            this.Name = "SteeringBoundary";
            this.Size = new System.Drawing.Size(397, 197);
            ((System.ComponentModel.ISupportInitialize)(this.maxAngularWarping)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.corneringWeight)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.angularAcceleration)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.linearAcceleration)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.linearVelocity)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.maxAngularVelocity)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.corneringPenalty)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.maxLinearWarping)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.stoppingDistance)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.NumericUpDown maxAngularWarping;
        private System.Windows.Forms.NumericUpDown corneringWeight;
        private System.Windows.Forms.NumericUpDown angularAcceleration;
        private System.Windows.Forms.NumericUpDown linearAcceleration;
        private System.Windows.Forms.NumericUpDown linearVelocity;
        private System.Windows.Forms.NumericUpDown maxAngularVelocity;
        private System.Windows.Forms.NumericUpDown corneringPenalty;
        private System.Windows.Forms.NumericUpDown maxLinearWarping;
        private System.Windows.Forms.NumericUpDown stoppingDistance;
    }
}

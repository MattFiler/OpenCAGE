namespace OpenCAGE.ConfigEditors
{
    partial class HackingEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HackingEditor));
            this.hackDifficulties = new System.Windows.Forms.ComboBox();
            this.lvl1Max = new System.Windows.Forms.ComboBox();
            this.label11 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.lvl99Max = new System.Windows.Forms.ComboBox();
            this.lvl3Max = new System.Windows.Forms.ComboBox();
            this.label10 = new System.Windows.Forms.Label();
            this.lvl0Max = new System.Windows.Forms.ComboBox();
            this.lvl2Max = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.timer_countdown_seconds = new System.Windows.Forms.NumericUpDown();
            this.number_of_alarms = new System.Windows.Forms.NumericUpDown();
            this.number_of_rounds = new System.Windows.Forms.NumericUpDown();
            this.length_of_keycode = new System.Windows.Forms.NumericUpDown();
            this.selection_angle_increase_in_deg = new System.Windows.Forms.NumericUpDown();
            this.outer_selection_angle_in_deg = new System.Windows.Forms.NumericUpDown();
            this.inner_selection_angle_in_deg = new System.Windows.Forms.NumericUpDown();
            this.label12 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.helpBtn = new System.Windows.Forms.Button();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.timer_countdown_seconds)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.number_of_alarms)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.number_of_rounds)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.length_of_keycode)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.selection_angle_increase_in_deg)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.outer_selection_angle_in_deg)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.inner_selection_angle_in_deg)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // hackDifficulties
            // 
            this.hackDifficulties.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.hackDifficulties.FormattingEnabled = true;
            this.hackDifficulties.Location = new System.Drawing.Point(12, 38);
            this.hackDifficulties.Name = "hackDifficulties";
            this.hackDifficulties.Size = new System.Drawing.Size(397, 21);
            this.hackDifficulties.TabIndex = 318;
            this.hackDifficulties.SelectedIndexChanged += new System.EventHandler(this.hackDifficulties_SelectedIndexChanged);
            // 
            // lvl1Max
            // 
            this.lvl1Max.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.lvl1Max.FormattingEnabled = true;
            this.lvl1Max.Location = new System.Drawing.Point(12, 80);
            this.lvl1Max.Name = "lvl1Max";
            this.lvl1Max.Size = new System.Drawing.Size(187, 21);
            this.lvl1Max.TabIndex = 328;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(9, 64);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(66, 13);
            this.label11.TabIndex = 329;
            this.label11.Text = "Tool Level 1";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(219, 24);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(66, 13);
            this.label9.TabIndex = 327;
            this.label9.Text = "Tool Level 3";
            // 
            // lvl99Max
            // 
            this.lvl99Max.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.lvl99Max.FormattingEnabled = true;
            this.lvl99Max.Location = new System.Drawing.Point(222, 80);
            this.lvl99Max.Name = "lvl99Max";
            this.lvl99Max.Size = new System.Drawing.Size(187, 21);
            this.lvl99Max.TabIndex = 330;
            // 
            // lvl3Max
            // 
            this.lvl3Max.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.lvl3Max.FormattingEnabled = true;
            this.lvl3Max.Location = new System.Drawing.Point(222, 40);
            this.lvl3Max.Name = "lvl3Max";
            this.lvl3Max.Size = new System.Drawing.Size(187, 21);
            this.lvl3Max.TabIndex = 326;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(219, 64);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(72, 13);
            this.label10.TabIndex = 331;
            this.label10.Text = "Tool Level 99";
            // 
            // lvl0Max
            // 
            this.lvl0Max.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.lvl0Max.FormattingEnabled = true;
            this.lvl0Max.Location = new System.Drawing.Point(12, 40);
            this.lvl0Max.Name = "lvl0Max";
            this.lvl0Max.Size = new System.Drawing.Size(187, 21);
            this.lvl0Max.TabIndex = 324;
            // 
            // lvl2Max
            // 
            this.lvl2Max.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.lvl2Max.FormattingEnabled = true;
            this.lvl2Max.Location = new System.Drawing.Point(12, 120);
            this.lvl2Max.Name = "lvl2Max";
            this.lvl2Max.Size = new System.Drawing.Size(187, 21);
            this.lvl2Max.TabIndex = 332;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(220, 148);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(147, 13);
            this.label6.TabIndex = 243;
            this.label6.Text = "Timer Countdown In Seconds";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(9, 148);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(174, 13);
            this.label5.TabIndex = 241;
            this.label5.Text = "Selection Angle Increase (Degrees)";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(9, 109);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(159, 13);
            this.label3.TabIndex = 239;
            this.label3.Text = "Outer Selection Angle (Degrees)";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(219, 70);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(99, 13);
            this.label2.TabIndex = 324;
            this.label2.Text = "Length Of Keycode";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 70);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(157, 13);
            this.label1.TabIndex = 237;
            this.label1.Text = "Inner Selection Angle (Degrees)";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(220, 109);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(92, 13);
            this.label4.TabIndex = 326;
            this.label4.Text = "Number Of Alarms";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(9, 187);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(98, 13);
            this.label7.TabIndex = 328;
            this.label7.Text = "Number Of Rounds";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.timer_countdown_seconds);
            this.groupBox2.Controls.Add(this.number_of_alarms);
            this.groupBox2.Controls.Add(this.number_of_rounds);
            this.groupBox2.Controls.Add(this.length_of_keycode);
            this.groupBox2.Controls.Add(this.selection_angle_increase_in_deg);
            this.groupBox2.Controls.Add(this.outer_selection_angle_in_deg);
            this.groupBox2.Controls.Add(this.inner_selection_angle_in_deg);
            this.groupBox2.Controls.Add(this.label12);
            this.groupBox2.Controls.Add(this.label7);
            this.groupBox2.Controls.Add(this.hackDifficulties);
            this.groupBox2.Controls.Add(this.label4);
            this.groupBox2.Controls.Add(this.label1);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.label5);
            this.groupBox2.Controls.Add(this.label6);
            this.groupBox2.Location = new System.Drawing.Point(12, 174);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(421, 236);
            this.groupBox2.TabIndex = 323;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Difficulty Settings";
            // 
            // timer_countdown_seconds
            // 
            this.timer_countdown_seconds.Location = new System.Drawing.Point(222, 165);
            this.timer_countdown_seconds.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.timer_countdown_seconds.Name = "timer_countdown_seconds";
            this.timer_countdown_seconds.Size = new System.Drawing.Size(187, 20);
            this.timer_countdown_seconds.TabIndex = 338;
            // 
            // number_of_alarms
            // 
            this.number_of_alarms.Location = new System.Drawing.Point(222, 125);
            this.number_of_alarms.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.number_of_alarms.Name = "number_of_alarms";
            this.number_of_alarms.Size = new System.Drawing.Size(187, 20);
            this.number_of_alarms.TabIndex = 340;
            // 
            // number_of_rounds
            // 
            this.number_of_rounds.Location = new System.Drawing.Point(12, 203);
            this.number_of_rounds.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.number_of_rounds.Name = "number_of_rounds";
            this.number_of_rounds.Size = new System.Drawing.Size(187, 20);
            this.number_of_rounds.TabIndex = 337;
            // 
            // length_of_keycode
            // 
            this.length_of_keycode.Location = new System.Drawing.Point(222, 86);
            this.length_of_keycode.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.length_of_keycode.Name = "length_of_keycode";
            this.length_of_keycode.Size = new System.Drawing.Size(187, 20);
            this.length_of_keycode.TabIndex = 339;
            // 
            // selection_angle_increase_in_deg
            // 
            this.selection_angle_increase_in_deg.Location = new System.Drawing.Point(12, 165);
            this.selection_angle_increase_in_deg.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.selection_angle_increase_in_deg.Name = "selection_angle_increase_in_deg";
            this.selection_angle_increase_in_deg.Size = new System.Drawing.Size(187, 20);
            this.selection_angle_increase_in_deg.TabIndex = 336;
            // 
            // outer_selection_angle_in_deg
            // 
            this.outer_selection_angle_in_deg.Location = new System.Drawing.Point(12, 125);
            this.outer_selection_angle_in_deg.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.outer_selection_angle_in_deg.Name = "outer_selection_angle_in_deg";
            this.outer_selection_angle_in_deg.Size = new System.Drawing.Size(187, 20);
            this.outer_selection_angle_in_deg.TabIndex = 335;
            // 
            // inner_selection_angle_in_deg
            // 
            this.inner_selection_angle_in_deg.Location = new System.Drawing.Point(12, 86);
            this.inner_selection_angle_in_deg.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.inner_selection_angle_in_deg.Name = "inner_selection_angle_in_deg";
            this.inner_selection_angle_in_deg.Size = new System.Drawing.Size(187, 20);
            this.inner_selection_angle_in_deg.TabIndex = 334;
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(9, 22);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(76, 13);
            this.label12.TabIndex = 330;
            this.label12.Text = "Difficulty Level";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(9, 104);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(66, 13);
            this.label13.TabIndex = 333;
            this.label13.Text = "Tool Level 2";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(9, 24);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(66, 13);
            this.label8.TabIndex = 325;
            this.label8.Text = "Tool Level 0";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label8);
            this.groupBox1.Controls.Add(this.lvl99Max);
            this.groupBox1.Controls.Add(this.label13);
            this.groupBox1.Controls.Add(this.lvl3Max);
            this.groupBox1.Controls.Add(this.label9);
            this.groupBox1.Controls.Add(this.label10);
            this.groupBox1.Controls.Add(this.lvl2Max);
            this.groupBox1.Controls.Add(this.label11);
            this.groupBox1.Controls.Add(this.lvl1Max);
            this.groupBox1.Controls.Add(this.lvl0Max);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(421, 156);
            this.groupBox1.TabIndex = 334;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Max Difficulties";
            // 
            // helpBtn
            // 
            this.helpBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.helpBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.helpBtn.Image = ((System.Drawing.Image)(resources.GetObject("helpBtn.Image")));
            this.helpBtn.Location = new System.Drawing.Point(422, 0);
            this.helpBtn.Name = "helpBtn";
            this.helpBtn.Size = new System.Drawing.Size(20, 20);
            this.helpBtn.TabIndex = 372;
            this.helpBtn.UseVisualStyleBackColor = true;
            this.helpBtn.Click += new System.EventHandler(this.helpBtn_Click);
            // 
            // HackingEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(442, 421);
            this.Controls.Add(this.helpBtn);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.groupBox2);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "HackingEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Hack Tool Editor";
            this.Load += new System.EventHandler(this.HackingEditor_Load);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.timer_countdown_seconds)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.number_of_alarms)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.number_of_rounds)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.length_of_keycode)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.selection_angle_increase_in_deg)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.outer_selection_angle_in_deg)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.inner_selection_angle_in_deg)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.ComboBox lvl1Max;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.ComboBox lvl99Max;
        private System.Windows.Forms.ComboBox lvl3Max;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.ComboBox lvl0Max;
        private System.Windows.Forms.ComboBox lvl2Max;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox hackDifficulties;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.NumericUpDown timer_countdown_seconds;
        private System.Windows.Forms.NumericUpDown number_of_alarms;
        private System.Windows.Forms.NumericUpDown number_of_rounds;
        private System.Windows.Forms.NumericUpDown length_of_keycode;
        private System.Windows.Forms.NumericUpDown selection_angle_increase_in_deg;
        private System.Windows.Forms.NumericUpDown outer_selection_angle_in_deg;
        private System.Windows.Forms.NumericUpDown inner_selection_angle_in_deg;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button helpBtn;
    }
}

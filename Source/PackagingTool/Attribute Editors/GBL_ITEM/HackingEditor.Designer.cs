namespace Alien_Isolation_Mod_Tools
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HackingEditor));
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label43 = new System.Windows.Forms.Label();
            this.label25 = new System.Windows.Forms.Label();
            this.max_difficulty_0 = new System.Windows.Forms.TextBox();
            this.max_difficulty_99 = new System.Windows.Forms.TextBox();
            this.label42 = new System.Windows.Forms.Label();
            this.min_difficulty_99 = new System.Windows.Forms.TextBox();
            this.max_difficulty_1 = new System.Windows.Forms.TextBox();
            this.label32 = new System.Windows.Forms.Label();
            this.label41 = new System.Windows.Forms.Label();
            this.max_difficulty_2 = new System.Windows.Forms.TextBox();
            this.max_difficulty_3 = new System.Windows.Forms.TextBox();
            this.label40 = new System.Windows.Forms.Label();
            this.label78 = new System.Windows.Forms.Label();
            this.btnSave = new System.Windows.Forms.Button();
            this.hackDifficulties = new System.Windows.Forms.ComboBox();
            this.btnSelectClass = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.number_of_rounds = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.number_of_alarms = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.length_of_keycode = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.inner_selection_angle_in_deg = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.outer_selection_angle_in_deg = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.selection_angle_increase_in_deg = new System.Windows.Forms.TextBox();
            this.timer_countdown_seconds = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label43);
            this.groupBox1.Controls.Add(this.label25);
            this.groupBox1.Controls.Add(this.max_difficulty_0);
            this.groupBox1.Controls.Add(this.max_difficulty_99);
            this.groupBox1.Controls.Add(this.label42);
            this.groupBox1.Controls.Add(this.min_difficulty_99);
            this.groupBox1.Controls.Add(this.max_difficulty_1);
            this.groupBox1.Controls.Add(this.label32);
            this.groupBox1.Controls.Add(this.label41);
            this.groupBox1.Controls.Add(this.max_difficulty_2);
            this.groupBox1.Controls.Add(this.max_difficulty_3);
            this.groupBox1.Controls.Add(this.label40);
            this.groupBox1.Location = new System.Drawing.Point(13, 41);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(439, 155);
            this.groupBox1.TabIndex = 322;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Tool Level Difficulties";
            // 
            // label43
            // 
            this.label43.AutoSize = true;
            this.label43.Location = new System.Drawing.Point(17, 26);
            this.label43.Name = "label43";
            this.label43.Size = new System.Drawing.Size(150, 13);
            this.label43.TabIndex = 237;
            this.label43.Text = "Max Difficulty For Tool Level 0";
            // 
            // label25
            // 
            this.label25.AutoSize = true;
            this.label25.Location = new System.Drawing.Point(286, 84);
            this.label25.Name = "label25";
            this.label25.Size = new System.Drawing.Size(77, 13);
            this.label25.TabIndex = 315;
            this.label25.Text = "Min -------> Max";
            // 
            // max_difficulty_0
            // 
            this.max_difficulty_0.Enabled = false;
            this.max_difficulty_0.Location = new System.Drawing.Point(20, 42);
            this.max_difficulty_0.Name = "max_difficulty_0";
            this.max_difficulty_0.Size = new System.Drawing.Size(187, 20);
            this.max_difficulty_0.TabIndex = 238;
            this.toolTip1.SetToolTip(this.max_difficulty_0, "The maximum difficulty level (see all below) for hack tool level 0.");
            // 
            // max_difficulty_99
            // 
            this.max_difficulty_99.Enabled = false;
            this.max_difficulty_99.Location = new System.Drawing.Point(369, 81);
            this.max_difficulty_99.Name = "max_difficulty_99";
            this.max_difficulty_99.Size = new System.Drawing.Size(49, 20);
            this.max_difficulty_99.TabIndex = 314;
            this.toolTip1.SetToolTip(this.max_difficulty_99, "The maximum difficulty level (see all below) for hack tool level \"99\".");
            // 
            // label42
            // 
            this.label42.AutoSize = true;
            this.label42.Location = new System.Drawing.Point(17, 65);
            this.label42.Name = "label42";
            this.label42.Size = new System.Drawing.Size(150, 13);
            this.label42.TabIndex = 239;
            this.label42.Text = "Max Difficulty For Tool Level 1";
            // 
            // min_difficulty_99
            // 
            this.min_difficulty_99.Enabled = false;
            this.min_difficulty_99.Location = new System.Drawing.Point(231, 82);
            this.min_difficulty_99.Name = "min_difficulty_99";
            this.min_difficulty_99.Size = new System.Drawing.Size(49, 20);
            this.min_difficulty_99.TabIndex = 313;
            this.toolTip1.SetToolTip(this.min_difficulty_99, "The minimum difficulty level (see all below) for hack tool level \"99\".");
            // 
            // max_difficulty_1
            // 
            this.max_difficulty_1.Enabled = false;
            this.max_difficulty_1.Location = new System.Drawing.Point(20, 81);
            this.max_difficulty_1.Name = "max_difficulty_1";
            this.max_difficulty_1.Size = new System.Drawing.Size(187, 20);
            this.max_difficulty_1.TabIndex = 240;
            this.toolTip1.SetToolTip(this.max_difficulty_1, "The maximum difficulty level (see all below) for hack tool level 1.");
            // 
            // label32
            // 
            this.label32.AutoSize = true;
            this.label32.Location = new System.Drawing.Point(227, 65);
            this.label32.Name = "label32";
            this.label32.Size = new System.Drawing.Size(133, 13);
            this.label32.TabIndex = 312;
            this.label32.Text = "Difficulty For Tool Level 99";
            // 
            // label41
            // 
            this.label41.AutoSize = true;
            this.label41.Location = new System.Drawing.Point(17, 104);
            this.label41.Name = "label41";
            this.label41.Size = new System.Drawing.Size(150, 13);
            this.label41.TabIndex = 241;
            this.label41.Text = "Max Difficulty For Tool Level 2";
            // 
            // max_difficulty_2
            // 
            this.max_difficulty_2.Enabled = false;
            this.max_difficulty_2.Location = new System.Drawing.Point(20, 120);
            this.max_difficulty_2.Name = "max_difficulty_2";
            this.max_difficulty_2.Size = new System.Drawing.Size(187, 20);
            this.max_difficulty_2.TabIndex = 242;
            this.toolTip1.SetToolTip(this.max_difficulty_2, "The maximum difficulty level (see all below) for hack tool level 2.");
            // 
            // max_difficulty_3
            // 
            this.max_difficulty_3.Enabled = false;
            this.max_difficulty_3.Location = new System.Drawing.Point(230, 42);
            this.max_difficulty_3.Name = "max_difficulty_3";
            this.max_difficulty_3.Size = new System.Drawing.Size(187, 20);
            this.max_difficulty_3.TabIndex = 244;
            this.toolTip1.SetToolTip(this.max_difficulty_3, "The maximum difficulty level (see all below) for hack tool level 3.");
            // 
            // label40
            // 
            this.label40.AutoSize = true;
            this.label40.Location = new System.Drawing.Point(227, 26);
            this.label40.Name = "label40";
            this.label40.Size = new System.Drawing.Size(150, 13);
            this.label40.TabIndex = 243;
            this.label40.Text = "Max Difficulty For Tool Level 3";
            // 
            // label78
            // 
            this.label78.AutoSize = true;
            this.label78.Font = new System.Drawing.Font("Microsoft Sans Serif", 17F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label78.Location = new System.Drawing.Point(48, 9);
            this.label78.Name = "label78";
            this.label78.Size = new System.Drawing.Size(355, 29);
            this.label78.TabIndex = 321;
            this.label78.Text = "Alien: Isolation Hack Tool Editor";
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(302, 428);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(150, 35);
            this.btnSave.TabIndex = 319;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // hackDifficulties
            // 
            this.hackDifficulties.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.hackDifficulties.Enabled = false;
            this.hackDifficulties.FormattingEnabled = true;
            this.hackDifficulties.Items.AddRange(new object[] {
            "DEFAULTS",
            "THE_PLAYER",
            "ALIEN",
            "ANDROID",
            "CIVILIAN",
            "SECURITY_GUARD",
            "CUTSCENE",
            "FACEHUGGER",
            "SPACESUIT_NPC",
            "RIOT_GUARD",
            "ANDROID_HEAVY",
            "MELEE_HUMAN",
            "INNOCENT",
            "CUTSCENE_ANDROID"});
            this.hackDifficulties.Location = new System.Drawing.Point(13, 202);
            this.hackDifficulties.Name = "hackDifficulties";
            this.hackDifficulties.Size = new System.Drawing.Size(327, 21);
            this.hackDifficulties.TabIndex = 318;
            this.toolTip1.SetToolTip(this.hackDifficulties, "All available hacking game difficulty levels.");
            // 
            // btnSelectClass
            // 
            this.btnSelectClass.Enabled = false;
            this.btnSelectClass.Location = new System.Drawing.Point(346, 201);
            this.btnSelectClass.Name = "btnSelectClass";
            this.btnSelectClass.Size = new System.Drawing.Size(106, 23);
            this.btnSelectClass.TabIndex = 317;
            this.btnSelectClass.Text = "Load Difficulty";
            this.toolTip1.SetToolTip(this.btnSelectClass, "Load selected hack difficulty level.");
            this.btnSelectClass.UseVisualStyleBackColor = true;
            this.btnSelectClass.Click += new System.EventHandler(this.btnSelectClass_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.number_of_rounds);
            this.groupBox2.Controls.Add(this.label7);
            this.groupBox2.Controls.Add(this.number_of_alarms);
            this.groupBox2.Controls.Add(this.label4);
            this.groupBox2.Controls.Add(this.length_of_keycode);
            this.groupBox2.Controls.Add(this.label1);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.inner_selection_angle_in_deg);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.outer_selection_angle_in_deg);
            this.groupBox2.Controls.Add(this.label5);
            this.groupBox2.Controls.Add(this.selection_angle_increase_in_deg);
            this.groupBox2.Controls.Add(this.timer_countdown_seconds);
            this.groupBox2.Controls.Add(this.label6);
            this.groupBox2.Location = new System.Drawing.Point(13, 230);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(439, 192);
            this.groupBox2.TabIndex = 323;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Difficulty Settings";
            // 
            // number_of_rounds
            // 
            this.number_of_rounds.Enabled = false;
            this.number_of_rounds.Location = new System.Drawing.Point(20, 159);
            this.number_of_rounds.Name = "number_of_rounds";
            this.number_of_rounds.Size = new System.Drawing.Size(187, 20);
            this.number_of_rounds.TabIndex = 329;
            this.toolTip1.SetToolTip(this.number_of_rounds, "How many rounds do we have in the game?");
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(17, 143);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(98, 13);
            this.label7.TabIndex = 328;
            this.label7.Text = "Number Of Rounds";
            // 
            // number_of_alarms
            // 
            this.number_of_alarms.Enabled = false;
            this.number_of_alarms.Location = new System.Drawing.Point(231, 81);
            this.number_of_alarms.Name = "number_of_alarms";
            this.number_of_alarms.Size = new System.Drawing.Size(187, 20);
            this.number_of_alarms.TabIndex = 327;
            this.toolTip1.SetToolTip(this.number_of_alarms, "How many alarms do we have in the game?");
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(228, 65);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(92, 13);
            this.label4.TabIndex = 326;
            this.label4.Text = "Number Of Alarms";
            // 
            // length_of_keycode
            // 
            this.length_of_keycode.Enabled = false;
            this.length_of_keycode.Location = new System.Drawing.Point(230, 42);
            this.length_of_keycode.Name = "length_of_keycode";
            this.length_of_keycode.Size = new System.Drawing.Size(187, 20);
            this.length_of_keycode.TabIndex = 325;
            this.toolTip1.SetToolTip(this.length_of_keycode, "What is the length of the keycode?");
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(17, 26);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(157, 13);
            this.label1.TabIndex = 237;
            this.label1.Text = "Inner Selection Angle (Degrees)";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(227, 26);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(99, 13);
            this.label2.TabIndex = 324;
            this.label2.Text = "Length Of Keycode";
            // 
            // inner_selection_angle_in_deg
            // 
            this.inner_selection_angle_in_deg.Enabled = false;
            this.inner_selection_angle_in_deg.Location = new System.Drawing.Point(20, 42);
            this.inner_selection_angle_in_deg.Name = "inner_selection_angle_in_deg";
            this.inner_selection_angle_in_deg.Size = new System.Drawing.Size(187, 20);
            this.inner_selection_angle_in_deg.TabIndex = 238;
            this.toolTip1.SetToolTip(this.inner_selection_angle_in_deg, "This is the angle used for the inner selection angle.");
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(17, 65);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(159, 13);
            this.label3.TabIndex = 239;
            this.label3.Text = "Outer Selection Angle (Degrees)";
            // 
            // outer_selection_angle_in_deg
            // 
            this.outer_selection_angle_in_deg.Enabled = false;
            this.outer_selection_angle_in_deg.Location = new System.Drawing.Point(20, 81);
            this.outer_selection_angle_in_deg.Name = "outer_selection_angle_in_deg";
            this.outer_selection_angle_in_deg.Size = new System.Drawing.Size(187, 20);
            this.outer_selection_angle_in_deg.TabIndex = 240;
            this.toolTip1.SetToolTip(this.outer_selection_angle_in_deg, "This is the angle used for the outer selection angle.");
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(17, 104);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(174, 13);
            this.label5.TabIndex = 241;
            this.label5.Text = "Selection Angle Increase (Degrees)";
            // 
            // selection_angle_increase_in_deg
            // 
            this.selection_angle_increase_in_deg.Enabled = false;
            this.selection_angle_increase_in_deg.Location = new System.Drawing.Point(20, 120);
            this.selection_angle_increase_in_deg.Name = "selection_angle_increase_in_deg";
            this.selection_angle_increase_in_deg.Size = new System.Drawing.Size(187, 20);
            this.selection_angle_increase_in_deg.TabIndex = 242;
            this.toolTip1.SetToolTip(this.selection_angle_increase_in_deg, "Once the player has found the selection range and has had the selector in that ar" +
        "ea for long enough the first grace will be increased by this amount.");
            // 
            // timer_countdown_seconds
            // 
            this.timer_countdown_seconds.Enabled = false;
            this.timer_countdown_seconds.Location = new System.Drawing.Point(231, 120);
            this.timer_countdown_seconds.Name = "timer_countdown_seconds";
            this.timer_countdown_seconds.Size = new System.Drawing.Size(187, 20);
            this.timer_countdown_seconds.TabIndex = 244;
            this.toolTip1.SetToolTip(this.timer_countdown_seconds, "How long does the player get for the timer?");
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(228, 104);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(147, 13);
            this.label6.TabIndex = 243;
            this.label6.Text = "Timer Countdown In Seconds";
            // 
            // HackingEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(461, 472);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.label78);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.hackDifficulties);
            this.Controls.Add(this.btnSelectClass);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "HackingEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Alien: Isolation Hack Tool Editor";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label43;
        private System.Windows.Forms.Label label25;
        private System.Windows.Forms.TextBox max_difficulty_0;
        private System.Windows.Forms.TextBox max_difficulty_99;
        private System.Windows.Forms.Label label42;
        private System.Windows.Forms.TextBox min_difficulty_99;
        private System.Windows.Forms.TextBox max_difficulty_1;
        private System.Windows.Forms.Label label32;
        private System.Windows.Forms.Label label41;
        private System.Windows.Forms.TextBox max_difficulty_2;
        private System.Windows.Forms.TextBox max_difficulty_3;
        private System.Windows.Forms.Label label40;
        private System.Windows.Forms.Label label78;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.ComboBox hackDifficulties;
        private System.Windows.Forms.Button btnSelectClass;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TextBox number_of_rounds;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox number_of_alarms;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox length_of_keycode;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox inner_selection_angle_in_deg;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox outer_selection_angle_in_deg;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox selection_angle_increase_in_deg;
        private System.Windows.Forms.TextBox timer_countdown_seconds;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
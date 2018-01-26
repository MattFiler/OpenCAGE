namespace Alien_Isolation_Mod_Tools
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
            this.loadChar = new System.Windows.Forms.Button();
            this.label78 = new System.Windows.Forms.Label();
            this.btnSave = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.loadSet = new System.Windows.Forms.Button();
            this.label8 = new System.Windows.Forms.Label();
            this.corneringPenalty1 = new System.Windows.Forms.TextBox();
            this.label11 = new System.Windows.Forms.Label();
            this.maxAngularWarping1 = new System.Windows.Forms.TextBox();
            this.label12 = new System.Windows.Forms.Label();
            this.setList = new System.Windows.Forms.ComboBox();
            this.stoppingDistance1 = new System.Windows.Forms.TextBox();
            this.label13 = new System.Windows.Forms.Label();
            this.corneringWeight1 = new System.Windows.Forms.TextBox();
            this.label14 = new System.Windows.Forms.Label();
            this.maxLinearWarping1 = new System.Windows.Forms.TextBox();
            this.label15 = new System.Windows.Forms.Label();
            this.angularAcceleration1 = new System.Windows.Forms.TextBox();
            this.label16 = new System.Windows.Forms.Label();
            this.maxAngularVelocity1 = new System.Windows.Forms.TextBox();
            this.label17 = new System.Windows.Forms.Label();
            this.linearAcceleration1 = new System.Windows.Forms.TextBox();
            this.label18 = new System.Windows.Forms.Label();
            this.linearVelocity1 = new System.Windows.Forms.TextBox();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.label34 = new System.Windows.Forms.Label();
            this.permittedLocomotionModulation = new System.Windows.Forms.TextBox();
            this.label35 = new System.Windows.Forms.Label();
            this.capsuleRadius = new System.Windows.Forms.TextBox();
            this.label36 = new System.Windows.Forms.Label();
            this.capsuleHeight = new System.Windows.Forms.TextBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label1 = new System.Windows.Forms.Label();
            this.corneringPenalty2 = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.maxAngularWarping2 = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.stoppingDistance2 = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.corneringWeight2 = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.maxLinearWarping2 = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.angularAcceleration2 = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.maxAngularVelocity2 = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.linearAcceleration2 = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.linearVelocity2 = new System.Windows.Forms.TextBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.label19 = new System.Windows.Forms.Label();
            this.corneringPenalty3 = new System.Windows.Forms.TextBox();
            this.label20 = new System.Windows.Forms.Label();
            this.maxAngularWarping3 = new System.Windows.Forms.TextBox();
            this.label21 = new System.Windows.Forms.Label();
            this.stoppingDistance3 = new System.Windows.Forms.TextBox();
            this.label22 = new System.Windows.Forms.Label();
            this.corneringWeight3 = new System.Windows.Forms.TextBox();
            this.label23 = new System.Windows.Forms.Label();
            this.maxLinearWarping3 = new System.Windows.Forms.TextBox();
            this.label24 = new System.Windows.Forms.Label();
            this.angularAcceleration3 = new System.Windows.Forms.TextBox();
            this.label25 = new System.Windows.Forms.Label();
            this.maxAngularVelocity3 = new System.Windows.Forms.TextBox();
            this.label26 = new System.Windows.Forms.Label();
            this.linearAcceleration3 = new System.Windows.Forms.TextBox();
            this.label27 = new System.Windows.Forms.Label();
            this.linearVelocity3 = new System.Windows.Forms.TextBox();
            this.groupBox1.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // characters
            // 
            this.characters.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.characters.FormattingEnabled = true;
            this.characters.Items.AddRange(new object[] {
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
            this.characters.Location = new System.Drawing.Point(437, 54);
            this.characters.Name = "characters";
            this.characters.Size = new System.Drawing.Size(291, 21);
            this.characters.TabIndex = 523;
            // 
            // loadChar
            // 
            this.loadChar.Location = new System.Drawing.Point(734, 53);
            this.loadChar.Name = "loadChar";
            this.loadChar.Size = new System.Drawing.Size(122, 23);
            this.loadChar.TabIndex = 522;
            this.loadChar.Text = "Load Character";
            this.loadChar.UseVisualStyleBackColor = true;
            this.loadChar.Click += new System.EventHandler(this.loadChar_Click);
            // 
            // label78
            // 
            this.label78.AutoSize = true;
            this.label78.Font = new System.Drawing.Font("Microsoft Sans Serif", 17F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label78.Location = new System.Drawing.Point(200, 9);
            this.label78.Name = "label78";
            this.label78.Size = new System.Drawing.Size(481, 29);
            this.label78.TabIndex = 519;
            this.label78.Text = "Alien: Isolation Character Locomotion Editor";
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(649, 476);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(207, 35);
            this.btnSave.TabIndex = 518;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label8);
            this.groupBox1.Controls.Add(this.corneringPenalty1);
            this.groupBox1.Controls.Add(this.label11);
            this.groupBox1.Controls.Add(this.maxAngularWarping1);
            this.groupBox1.Controls.Add(this.label12);
            this.groupBox1.Controls.Add(this.stoppingDistance1);
            this.groupBox1.Controls.Add(this.label13);
            this.groupBox1.Controls.Add(this.corneringWeight1);
            this.groupBox1.Controls.Add(this.label14);
            this.groupBox1.Controls.Add(this.maxLinearWarping1);
            this.groupBox1.Controls.Add(this.label15);
            this.groupBox1.Controls.Add(this.angularAcceleration1);
            this.groupBox1.Controls.Add(this.label16);
            this.groupBox1.Controls.Add(this.maxAngularVelocity1);
            this.groupBox1.Controls.Add(this.label17);
            this.groupBox1.Controls.Add(this.linearAcceleration1);
            this.groupBox1.Controls.Add(this.label18);
            this.groupBox1.Controls.Add(this.linearVelocity1);
            this.groupBox1.Location = new System.Drawing.Point(12, 47);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(419, 229);
            this.groupBox1.TabIndex = 527;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Steering Boundary Data";
            // 
            // loadSet
            // 
            this.loadSet.Enabled = false;
            this.loadSet.Location = new System.Drawing.Point(734, 82);
            this.loadSet.Name = "loadSet";
            this.loadSet.Size = new System.Drawing.Size(122, 23);
            this.loadSet.TabIndex = 526;
            this.loadSet.Text = "Load Set";
            this.loadSet.UseVisualStyleBackColor = true;
            this.loadSet.Click += new System.EventHandler(this.loadSet_Click);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(13, 140);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(102, 13);
            this.label8.TabIndex = 525;
            this.label8.Text = "Max Linear Warping";
            // 
            // corneringPenalty1
            // 
            this.corneringPenalty1.Enabled = false;
            this.corneringPenalty1.Location = new System.Drawing.Point(16, 117);
            this.corneringPenalty1.Name = "corneringPenalty1";
            this.corneringPenalty1.Size = new System.Drawing.Size(187, 20);
            this.corneringPenalty1.TabIndex = 526;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(210, 140);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(109, 13);
            this.label11.TabIndex = 523;
            this.label11.Text = "Max Angular Warping";
            // 
            // maxAngularWarping1
            // 
            this.maxAngularWarping1.Enabled = false;
            this.maxAngularWarping1.Location = new System.Drawing.Point(213, 156);
            this.maxAngularWarping1.Name = "maxAngularWarping1";
            this.maxAngularWarping1.Size = new System.Drawing.Size(187, 20);
            this.maxAngularWarping1.TabIndex = 524;
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(13, 101);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(90, 13);
            this.label12.TabIndex = 521;
            this.label12.Text = "Cornering Penalty";
            // 
            // setList
            // 
            this.setList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.setList.Enabled = false;
            this.setList.FormattingEnabled = true;
            this.setList.Items.AddRange(new object[] {
            "1",
            "2",
            "3",
            "4"});
            this.setList.Location = new System.Drawing.Point(437, 83);
            this.setList.Name = "setList";
            this.setList.Size = new System.Drawing.Size(291, 21);
            this.setList.TabIndex = 517;
            // 
            // stoppingDistance1
            // 
            this.stoppingDistance1.Enabled = false;
            this.stoppingDistance1.Location = new System.Drawing.Point(16, 195);
            this.stoppingDistance1.Name = "stoppingDistance1";
            this.stoppingDistance1.Size = new System.Drawing.Size(187, 20);
            this.stoppingDistance1.TabIndex = 522;
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(210, 101);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(89, 13);
            this.label13.TabIndex = 519;
            this.label13.Text = "Cornering Weight";
            // 
            // corneringWeight1
            // 
            this.corneringWeight1.Enabled = false;
            this.corneringWeight1.Location = new System.Drawing.Point(213, 117);
            this.corneringWeight1.Name = "corneringWeight1";
            this.corneringWeight1.Size = new System.Drawing.Size(187, 20);
            this.corneringWeight1.TabIndex = 520;
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(13, 179);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(94, 13);
            this.label14.TabIndex = 517;
            this.label14.Text = "Stopping Distance";
            // 
            // maxLinearWarping1
            // 
            this.maxLinearWarping1.Enabled = false;
            this.maxLinearWarping1.Location = new System.Drawing.Point(16, 156);
            this.maxLinearWarping1.Name = "maxLinearWarping1";
            this.maxLinearWarping1.Size = new System.Drawing.Size(187, 20);
            this.maxLinearWarping1.TabIndex = 518;
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(210, 62);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(105, 13);
            this.label15.TabIndex = 515;
            this.label15.Text = "Angular Acceleration";
            // 
            // angularAcceleration1
            // 
            this.angularAcceleration1.Enabled = false;
            this.angularAcceleration1.Location = new System.Drawing.Point(213, 78);
            this.angularAcceleration1.Name = "angularAcceleration1";
            this.angularAcceleration1.Size = new System.Drawing.Size(187, 20);
            this.angularAcceleration1.TabIndex = 516;
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(13, 62);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(106, 13);
            this.label16.TabIndex = 513;
            this.label16.Text = "Max Angular Velocity";
            // 
            // maxAngularVelocity1
            // 
            this.maxAngularVelocity1.Enabled = false;
            this.maxAngularVelocity1.Location = new System.Drawing.Point(16, 78);
            this.maxAngularVelocity1.Name = "maxAngularVelocity1";
            this.maxAngularVelocity1.Size = new System.Drawing.Size(187, 20);
            this.maxAngularVelocity1.TabIndex = 514;
            // 
            // label17
            // 
            this.label17.AutoSize = true;
            this.label17.Location = new System.Drawing.Point(210, 23);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(98, 13);
            this.label17.TabIndex = 511;
            this.label17.Text = "Linear Acceleration";
            // 
            // linearAcceleration1
            // 
            this.linearAcceleration1.Enabled = false;
            this.linearAcceleration1.Location = new System.Drawing.Point(213, 39);
            this.linearAcceleration1.Name = "linearAcceleration1";
            this.linearAcceleration1.Size = new System.Drawing.Size(187, 20);
            this.linearAcceleration1.TabIndex = 512;
            // 
            // label18
            // 
            this.label18.AutoSize = true;
            this.label18.Location = new System.Drawing.Point(13, 23);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(76, 13);
            this.label18.TabIndex = 509;
            this.label18.Text = "Linear Velocity";
            // 
            // linearVelocity1
            // 
            this.linearVelocity1.Enabled = false;
            this.linearVelocity1.Location = new System.Drawing.Point(16, 39);
            this.linearVelocity1.Name = "linearVelocity1";
            this.linearVelocity1.Size = new System.Drawing.Size(187, 20);
            this.linearVelocity1.TabIndex = 510;
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.label34);
            this.groupBox4.Controls.Add(this.permittedLocomotionModulation);
            this.groupBox4.Controls.Add(this.label35);
            this.groupBox4.Controls.Add(this.capsuleRadius);
            this.groupBox4.Controls.Add(this.label36);
            this.groupBox4.Controls.Add(this.capsuleHeight);
            this.groupBox4.Location = new System.Drawing.Point(437, 355);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(419, 115);
            this.groupBox4.TabIndex = 528;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Misc Locomotion Settings";
            // 
            // label34
            // 
            this.label34.AutoSize = true;
            this.label34.Location = new System.Drawing.Point(13, 62);
            this.label34.Name = "label34";
            this.label34.Size = new System.Drawing.Size(117, 13);
            this.label34.TabIndex = 513;
            this.label34.Text = "Locomotion Modulation";
            // 
            // permittedLocomotionModulation
            // 
            this.permittedLocomotionModulation.Enabled = false;
            this.permittedLocomotionModulation.Location = new System.Drawing.Point(16, 78);
            this.permittedLocomotionModulation.Name = "permittedLocomotionModulation";
            this.permittedLocomotionModulation.Size = new System.Drawing.Size(187, 20);
            this.permittedLocomotionModulation.TabIndex = 514;
            // 
            // label35
            // 
            this.label35.AutoSize = true;
            this.label35.Location = new System.Drawing.Point(210, 23);
            this.label35.Name = "label35";
            this.label35.Size = new System.Drawing.Size(81, 13);
            this.label35.TabIndex = 511;
            this.label35.Text = "Capsule Radius";
            // 
            // capsuleRadius
            // 
            this.capsuleRadius.Enabled = false;
            this.capsuleRadius.Location = new System.Drawing.Point(213, 39);
            this.capsuleRadius.Name = "capsuleRadius";
            this.capsuleRadius.Size = new System.Drawing.Size(187, 20);
            this.capsuleRadius.TabIndex = 512;
            // 
            // label36
            // 
            this.label36.AutoSize = true;
            this.label36.Location = new System.Drawing.Point(13, 23);
            this.label36.Name = "label36";
            this.label36.Size = new System.Drawing.Size(79, 13);
            this.label36.TabIndex = 509;
            this.label36.Text = "Capsule Height";
            // 
            // capsuleHeight
            // 
            this.capsuleHeight.Enabled = false;
            this.capsuleHeight.Location = new System.Drawing.Point(16, 39);
            this.capsuleHeight.Name = "capsuleHeight";
            this.capsuleHeight.Size = new System.Drawing.Size(187, 20);
            this.capsuleHeight.TabIndex = 510;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.label1);
            this.groupBox2.Controls.Add(this.corneringPenalty2);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.maxAngularWarping2);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.stoppingDistance2);
            this.groupBox2.Controls.Add(this.label4);
            this.groupBox2.Controls.Add(this.corneringWeight2);
            this.groupBox2.Controls.Add(this.label5);
            this.groupBox2.Controls.Add(this.maxLinearWarping2);
            this.groupBox2.Controls.Add(this.label6);
            this.groupBox2.Controls.Add(this.angularAcceleration2);
            this.groupBox2.Controls.Add(this.label7);
            this.groupBox2.Controls.Add(this.maxAngularVelocity2);
            this.groupBox2.Controls.Add(this.label9);
            this.groupBox2.Controls.Add(this.linearAcceleration2);
            this.groupBox2.Controls.Add(this.label10);
            this.groupBox2.Controls.Add(this.linearVelocity2);
            this.groupBox2.Location = new System.Drawing.Point(12, 282);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(419, 229);
            this.groupBox2.TabIndex = 528;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Crouched Steering Boundary Data";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 140);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(102, 13);
            this.label1.TabIndex = 525;
            this.label1.Text = "Max Linear Warping";
            // 
            // corneringPenalty2
            // 
            this.corneringPenalty2.Enabled = false;
            this.corneringPenalty2.Location = new System.Drawing.Point(16, 117);
            this.corneringPenalty2.Name = "corneringPenalty2";
            this.corneringPenalty2.Size = new System.Drawing.Size(187, 20);
            this.corneringPenalty2.TabIndex = 526;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(210, 140);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(109, 13);
            this.label2.TabIndex = 523;
            this.label2.Text = "Max Angular Warping";
            // 
            // maxAngularWarping2
            // 
            this.maxAngularWarping2.Enabled = false;
            this.maxAngularWarping2.Location = new System.Drawing.Point(213, 156);
            this.maxAngularWarping2.Name = "maxAngularWarping2";
            this.maxAngularWarping2.Size = new System.Drawing.Size(187, 20);
            this.maxAngularWarping2.TabIndex = 524;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(13, 101);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(90, 13);
            this.label3.TabIndex = 521;
            this.label3.Text = "Cornering Penalty";
            // 
            // stoppingDistance2
            // 
            this.stoppingDistance2.Enabled = false;
            this.stoppingDistance2.Location = new System.Drawing.Point(16, 195);
            this.stoppingDistance2.Name = "stoppingDistance2";
            this.stoppingDistance2.Size = new System.Drawing.Size(187, 20);
            this.stoppingDistance2.TabIndex = 522;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(210, 101);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(89, 13);
            this.label4.TabIndex = 519;
            this.label4.Text = "Cornering Weight";
            // 
            // corneringWeight2
            // 
            this.corneringWeight2.Enabled = false;
            this.corneringWeight2.Location = new System.Drawing.Point(213, 117);
            this.corneringWeight2.Name = "corneringWeight2";
            this.corneringWeight2.Size = new System.Drawing.Size(187, 20);
            this.corneringWeight2.TabIndex = 520;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(13, 179);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(94, 13);
            this.label5.TabIndex = 517;
            this.label5.Text = "Stopping Distance";
            // 
            // maxLinearWarping2
            // 
            this.maxLinearWarping2.Enabled = false;
            this.maxLinearWarping2.Location = new System.Drawing.Point(16, 156);
            this.maxLinearWarping2.Name = "maxLinearWarping2";
            this.maxLinearWarping2.Size = new System.Drawing.Size(187, 20);
            this.maxLinearWarping2.TabIndex = 518;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(210, 62);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(105, 13);
            this.label6.TabIndex = 515;
            this.label6.Text = "Angular Acceleration";
            // 
            // angularAcceleration2
            // 
            this.angularAcceleration2.Enabled = false;
            this.angularAcceleration2.Location = new System.Drawing.Point(213, 78);
            this.angularAcceleration2.Name = "angularAcceleration2";
            this.angularAcceleration2.Size = new System.Drawing.Size(187, 20);
            this.angularAcceleration2.TabIndex = 516;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(13, 62);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(106, 13);
            this.label7.TabIndex = 513;
            this.label7.Text = "Max Angular Velocity";
            // 
            // maxAngularVelocity2
            // 
            this.maxAngularVelocity2.Enabled = false;
            this.maxAngularVelocity2.Location = new System.Drawing.Point(16, 78);
            this.maxAngularVelocity2.Name = "maxAngularVelocity2";
            this.maxAngularVelocity2.Size = new System.Drawing.Size(187, 20);
            this.maxAngularVelocity2.TabIndex = 514;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(210, 23);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(98, 13);
            this.label9.TabIndex = 511;
            this.label9.Text = "Linear Acceleration";
            // 
            // linearAcceleration2
            // 
            this.linearAcceleration2.Enabled = false;
            this.linearAcceleration2.Location = new System.Drawing.Point(213, 39);
            this.linearAcceleration2.Name = "linearAcceleration2";
            this.linearAcceleration2.Size = new System.Drawing.Size(187, 20);
            this.linearAcceleration2.TabIndex = 512;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(13, 23);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(76, 13);
            this.label10.TabIndex = 509;
            this.label10.Text = "Linear Velocity";
            // 
            // linearVelocity2
            // 
            this.linearVelocity2.Enabled = false;
            this.linearVelocity2.Location = new System.Drawing.Point(16, 39);
            this.linearVelocity2.Name = "linearVelocity2";
            this.linearVelocity2.Size = new System.Drawing.Size(187, 20);
            this.linearVelocity2.TabIndex = 510;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.label19);
            this.groupBox3.Controls.Add(this.corneringPenalty3);
            this.groupBox3.Controls.Add(this.label20);
            this.groupBox3.Controls.Add(this.maxAngularWarping3);
            this.groupBox3.Controls.Add(this.label21);
            this.groupBox3.Controls.Add(this.stoppingDistance3);
            this.groupBox3.Controls.Add(this.label22);
            this.groupBox3.Controls.Add(this.corneringWeight3);
            this.groupBox3.Controls.Add(this.label23);
            this.groupBox3.Controls.Add(this.maxLinearWarping3);
            this.groupBox3.Controls.Add(this.label24);
            this.groupBox3.Controls.Add(this.angularAcceleration3);
            this.groupBox3.Controls.Add(this.label25);
            this.groupBox3.Controls.Add(this.maxAngularVelocity3);
            this.groupBox3.Controls.Add(this.label26);
            this.groupBox3.Controls.Add(this.linearAcceleration3);
            this.groupBox3.Controls.Add(this.label27);
            this.groupBox3.Controls.Add(this.linearVelocity3);
            this.groupBox3.Location = new System.Drawing.Point(437, 120);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(419, 229);
            this.groupBox3.TabIndex = 528;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Aimed Steering Boundary Data";
            // 
            // label19
            // 
            this.label19.AutoSize = true;
            this.label19.Location = new System.Drawing.Point(13, 140);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(102, 13);
            this.label19.TabIndex = 525;
            this.label19.Text = "Max Linear Warping";
            // 
            // corneringPenalty3
            // 
            this.corneringPenalty3.Enabled = false;
            this.corneringPenalty3.Location = new System.Drawing.Point(16, 117);
            this.corneringPenalty3.Name = "corneringPenalty3";
            this.corneringPenalty3.Size = new System.Drawing.Size(187, 20);
            this.corneringPenalty3.TabIndex = 526;
            // 
            // label20
            // 
            this.label20.AutoSize = true;
            this.label20.Location = new System.Drawing.Point(210, 140);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(109, 13);
            this.label20.TabIndex = 523;
            this.label20.Text = "Max Angular Warping";
            // 
            // maxAngularWarping3
            // 
            this.maxAngularWarping3.Enabled = false;
            this.maxAngularWarping3.Location = new System.Drawing.Point(213, 156);
            this.maxAngularWarping3.Name = "maxAngularWarping3";
            this.maxAngularWarping3.Size = new System.Drawing.Size(187, 20);
            this.maxAngularWarping3.TabIndex = 524;
            // 
            // label21
            // 
            this.label21.AutoSize = true;
            this.label21.Location = new System.Drawing.Point(13, 101);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(90, 13);
            this.label21.TabIndex = 521;
            this.label21.Text = "Cornering Penalty";
            // 
            // stoppingDistance3
            // 
            this.stoppingDistance3.Enabled = false;
            this.stoppingDistance3.Location = new System.Drawing.Point(16, 195);
            this.stoppingDistance3.Name = "stoppingDistance3";
            this.stoppingDistance3.Size = new System.Drawing.Size(187, 20);
            this.stoppingDistance3.TabIndex = 522;
            // 
            // label22
            // 
            this.label22.AutoSize = true;
            this.label22.Location = new System.Drawing.Point(210, 101);
            this.label22.Name = "label22";
            this.label22.Size = new System.Drawing.Size(89, 13);
            this.label22.TabIndex = 519;
            this.label22.Text = "Cornering Weight";
            // 
            // corneringWeight3
            // 
            this.corneringWeight3.Enabled = false;
            this.corneringWeight3.Location = new System.Drawing.Point(213, 117);
            this.corneringWeight3.Name = "corneringWeight3";
            this.corneringWeight3.Size = new System.Drawing.Size(187, 20);
            this.corneringWeight3.TabIndex = 520;
            // 
            // label23
            // 
            this.label23.AutoSize = true;
            this.label23.Location = new System.Drawing.Point(13, 179);
            this.label23.Name = "label23";
            this.label23.Size = new System.Drawing.Size(94, 13);
            this.label23.TabIndex = 517;
            this.label23.Text = "Stopping Distance";
            // 
            // maxLinearWarping3
            // 
            this.maxLinearWarping3.Enabled = false;
            this.maxLinearWarping3.Location = new System.Drawing.Point(16, 156);
            this.maxLinearWarping3.Name = "maxLinearWarping3";
            this.maxLinearWarping3.Size = new System.Drawing.Size(187, 20);
            this.maxLinearWarping3.TabIndex = 518;
            // 
            // label24
            // 
            this.label24.AutoSize = true;
            this.label24.Location = new System.Drawing.Point(210, 62);
            this.label24.Name = "label24";
            this.label24.Size = new System.Drawing.Size(105, 13);
            this.label24.TabIndex = 515;
            this.label24.Text = "Angular Acceleration";
            // 
            // angularAcceleration3
            // 
            this.angularAcceleration3.Enabled = false;
            this.angularAcceleration3.Location = new System.Drawing.Point(213, 78);
            this.angularAcceleration3.Name = "angularAcceleration3";
            this.angularAcceleration3.Size = new System.Drawing.Size(187, 20);
            this.angularAcceleration3.TabIndex = 516;
            // 
            // label25
            // 
            this.label25.AutoSize = true;
            this.label25.Location = new System.Drawing.Point(13, 62);
            this.label25.Name = "label25";
            this.label25.Size = new System.Drawing.Size(106, 13);
            this.label25.TabIndex = 513;
            this.label25.Text = "Max Angular Velocity";
            // 
            // maxAngularVelocity3
            // 
            this.maxAngularVelocity3.Enabled = false;
            this.maxAngularVelocity3.Location = new System.Drawing.Point(16, 78);
            this.maxAngularVelocity3.Name = "maxAngularVelocity3";
            this.maxAngularVelocity3.Size = new System.Drawing.Size(187, 20);
            this.maxAngularVelocity3.TabIndex = 514;
            // 
            // label26
            // 
            this.label26.AutoSize = true;
            this.label26.Location = new System.Drawing.Point(210, 23);
            this.label26.Name = "label26";
            this.label26.Size = new System.Drawing.Size(98, 13);
            this.label26.TabIndex = 511;
            this.label26.Text = "Linear Acceleration";
            // 
            // linearAcceleration3
            // 
            this.linearAcceleration3.Enabled = false;
            this.linearAcceleration3.Location = new System.Drawing.Point(213, 39);
            this.linearAcceleration3.Name = "linearAcceleration3";
            this.linearAcceleration3.Size = new System.Drawing.Size(187, 20);
            this.linearAcceleration3.TabIndex = 512;
            // 
            // label27
            // 
            this.label27.AutoSize = true;
            this.label27.Location = new System.Drawing.Point(13, 23);
            this.label27.Name = "label27";
            this.label27.Size = new System.Drawing.Size(76, 13);
            this.label27.TabIndex = 509;
            this.label27.Text = "Linear Velocity";
            // 
            // linearVelocity3
            // 
            this.linearVelocity3.Enabled = false;
            this.linearVelocity3.Location = new System.Drawing.Point(16, 39);
            this.linearVelocity3.Name = "linearVelocity3";
            this.linearVelocity3.Size = new System.Drawing.Size(187, 20);
            this.linearVelocity3.TabIndex = 510;
            // 
            // LocomotionEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(868, 519);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.loadSet);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.characters);
            this.Controls.Add(this.loadChar);
            this.Controls.Add(this.setList);
            this.Controls.Add(this.label78);
            this.Controls.Add(this.btnSave);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "LocomotionEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Alien: Isolation Character Locomotion Editor";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ComboBox characters;
        private System.Windows.Forms.Button loadChar;
        private System.Windows.Forms.Label label78;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox corneringPenalty1;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.TextBox maxAngularWarping1;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.TextBox stoppingDistance1;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.TextBox corneringWeight1;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.TextBox maxLinearWarping1;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.TextBox angularAcceleration1;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.TextBox maxAngularVelocity1;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.TextBox linearAcceleration1;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.TextBox linearVelocity1;
        private System.Windows.Forms.Button loadSet;
        private System.Windows.Forms.ComboBox setList;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Label label34;
        private System.Windows.Forms.TextBox permittedLocomotionModulation;
        private System.Windows.Forms.Label label35;
        private System.Windows.Forms.TextBox capsuleRadius;
        private System.Windows.Forms.Label label36;
        private System.Windows.Forms.TextBox capsuleHeight;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox corneringPenalty2;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox maxAngularWarping2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox stoppingDistance2;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox corneringWeight2;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox maxLinearWarping2;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox angularAcceleration2;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox maxAngularVelocity2;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox linearAcceleration2;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox linearVelocity2;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.TextBox corneringPenalty3;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.TextBox maxAngularWarping3;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.TextBox stoppingDistance3;
        private System.Windows.Forms.Label label22;
        private System.Windows.Forms.TextBox corneringWeight3;
        private System.Windows.Forms.Label label23;
        private System.Windows.Forms.TextBox maxLinearWarping3;
        private System.Windows.Forms.Label label24;
        private System.Windows.Forms.TextBox angularAcceleration3;
        private System.Windows.Forms.Label label25;
        private System.Windows.Forms.TextBox maxAngularVelocity3;
        private System.Windows.Forms.Label label26;
        private System.Windows.Forms.TextBox linearAcceleration3;
        private System.Windows.Forms.Label label27;
        private System.Windows.Forms.TextBox linearVelocity3;
    }
}
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
            this.label8 = new System.Windows.Forms.Label();
            this.corneringPenalty1 = new System.Windows.Forms.TextBox();
            this.label11 = new System.Windows.Forms.Label();
            this.maxAngularWarping1 = new System.Windows.Forms.TextBox();
            this.label12 = new System.Windows.Forms.Label();
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
            this.loadSet = new System.Windows.Forms.Button();
            this.setList = new System.Windows.Forms.ComboBox();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.label34 = new System.Windows.Forms.Label();
            this.permittedLocomotionModulation = new System.Windows.Forms.TextBox();
            this.label35 = new System.Windows.Forms.Label();
            this.capsuleRadius = new System.Windows.Forms.TextBox();
            this.label36 = new System.Windows.Forms.Label();
            this.capsuleHeight = new System.Windows.Forms.TextBox();
            this.variantType = new System.Windows.Forms.ComboBox();
            this.swapVariant = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.groupBox4.SuspendLayout();
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
            this.characters.Location = new System.Drawing.Point(12, 51);
            this.characters.Name = "characters";
            this.characters.Size = new System.Drawing.Size(291, 21);
            this.characters.TabIndex = 523;
            // 
            // loadChar
            // 
            this.loadChar.Location = new System.Drawing.Point(309, 50);
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
            this.label78.Location = new System.Drawing.Point(34, 11);
            this.label78.Name = "label78";
            this.label78.Size = new System.Drawing.Size(371, 29);
            this.label78.TabIndex = 519;
            this.label78.Text = "Alien: Isolation Locomotion Editor";
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(224, 489);
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
            this.groupBox1.Location = new System.Drawing.Point(12, 254);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(419, 229);
            this.groupBox1.TabIndex = 527;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Steering Boundary Data";
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
            // loadSet
            // 
            this.loadSet.Enabled = false;
            this.loadSet.Location = new System.Drawing.Point(309, 199);
            this.loadSet.Name = "loadSet";
            this.loadSet.Size = new System.Drawing.Size(122, 23);
            this.loadSet.TabIndex = 526;
            this.loadSet.Text = "Load Set";
            this.loadSet.UseVisualStyleBackColor = true;
            this.loadSet.Click += new System.EventHandler(this.loadSet_Click);
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
            this.setList.Location = new System.Drawing.Point(12, 200);
            this.setList.Name = "setList";
            this.setList.Size = new System.Drawing.Size(291, 21);
            this.setList.TabIndex = 517;
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.label34);
            this.groupBox4.Controls.Add(this.permittedLocomotionModulation);
            this.groupBox4.Controls.Add(this.label35);
            this.groupBox4.Controls.Add(this.capsuleRadius);
            this.groupBox4.Controls.Add(this.label36);
            this.groupBox4.Controls.Add(this.capsuleHeight);
            this.groupBox4.Location = new System.Drawing.Point(12, 79);
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
            // variantType
            // 
            this.variantType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.variantType.Enabled = false;
            this.variantType.FormattingEnabled = true;
            this.variantType.Items.AddRange(new object[] {
            "Normal",
            "Crouched",
            "Aimed"});
            this.variantType.Location = new System.Drawing.Point(12, 227);
            this.variantType.Name = "variantType";
            this.variantType.Size = new System.Drawing.Size(291, 21);
            this.variantType.TabIndex = 529;
            // 
            // swapVariant
            // 
            this.swapVariant.Enabled = false;
            this.swapVariant.Location = new System.Drawing.Point(309, 226);
            this.swapVariant.Name = "swapVariant";
            this.swapVariant.Size = new System.Drawing.Size(122, 23);
            this.swapVariant.TabIndex = 530;
            this.swapVariant.Text = "Load Variant";
            this.swapVariant.UseVisualStyleBackColor = true;
            this.swapVariant.Click += new System.EventHandler(this.swapVariant_Click);
            // 
            // LocomotionEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(441, 533);
            this.Controls.Add(this.swapVariant);
            this.Controls.Add(this.variantType);
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
        private System.Windows.Forms.ComboBox variantType;
        private System.Windows.Forms.Button swapVariant;
    }
}
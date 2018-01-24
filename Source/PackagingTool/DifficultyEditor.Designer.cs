namespace PackagingTool
{
    partial class DifficultyEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DifficultyEditor));
            this.label78 = new System.Windows.Forms.Label();
            this.btnSave = new System.Windows.Forms.Button();
            this.classSelection = new System.Windows.Forms.ComboBox();
            this.btnSelectClass = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.comboBox2 = new System.Windows.Forms.ComboBox();
            this.button1 = new System.Windows.Forms.Button();
            this.comboBox3 = new System.Windows.Forms.ComboBox();
            this.button2 = new System.Windows.Forms.Button();
            this.comboBox4 = new System.Windows.Forms.ComboBox();
            this.button3 = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.label1 = new System.Windows.Forms.Label();
            this.visual_sense_exposure_effect_lower_modifier = new System.Windows.Forms.TextBox();
            this.visual_sense_exposure_effect_upper_modifier = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.visual_sense_stance_effect_lower_modifier = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.visual_sense_stance_effect_upper_modifier = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.damage_dealt_scalar = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.damage_received_scalar = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.attack_pace_modifier = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.suspicious_item_loop_scalar = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.weapon_sound_combined_sense_activation_modifier = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.visual_combined_sense_activation_modifier = new System.Windows.Forms.TextBox();
            this.label11 = new System.Windows.Forms.Label();
            this.weapon_sound_sense_activation_modifier = new System.Windows.Forms.TextBox();
            this.label12 = new System.Windows.Forms.Label();
            this.visual_sense_activation_modifier = new System.Windows.Forms.TextBox();
            this.label13 = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.flash_light_combined_sense_activation_modifier = new System.Windows.Forms.TextBox();
            this.movement_sound_sense_activation_modifier = new System.Windows.Forms.TextBox();
            this.label15 = new System.Windows.Forms.Label();
            this.flash_light_sense_activation_modifier = new System.Windows.Forms.TextBox();
            this.movement_sound_combined_sense_activation_modifier = new System.Windows.Forms.TextBox();
            this.label17 = new System.Windows.Forms.Label();
            this.max_hearing_distance_modifier = new System.Windows.Forms.TextBox();
            this.label22 = new System.Windows.Forms.Label();
            this.label23 = new System.Windows.Forms.Label();
            this.label24 = new System.Windows.Forms.Label();
            this.label26 = new System.Windows.Forms.Label();
            this.label27 = new System.Windows.Forms.Label();
            this.label29 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.SuspendLayout();
            // 
            // label78
            // 
            this.label78.AutoSize = true;
            this.label78.Font = new System.Drawing.Font("Microsoft Sans Serif", 17F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label78.Location = new System.Drawing.Point(59, 9);
            this.label78.Name = "label78";
            this.label78.Size = new System.Drawing.Size(334, 29);
            this.label78.TabIndex = 412;
            this.label78.Text = "Alien: Isolation Difficulty Editor";
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(201, 996);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(241, 35);
            this.btnSave.TabIndex = 410;
            this.btnSave.Text = "Save Difficulty Configuration";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // classSelection
            // 
            this.classSelection.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.classSelection.FormattingEnabled = true;
            this.classSelection.Items.AddRange(new object[] {
            "HARD",
            "MEDIUM",
            "EASY",
            "NOVICE",
            "IRON"});
            this.classSelection.Location = new System.Drawing.Point(15, 42);
            this.classSelection.Name = "classSelection";
            this.classSelection.Size = new System.Drawing.Size(327, 21);
            this.classSelection.TabIndex = 409;
            // 
            // btnSelectClass
            // 
            this.btnSelectClass.Location = new System.Drawing.Point(348, 41);
            this.btnSelectClass.Name = "btnSelectClass";
            this.btnSelectClass.Size = new System.Drawing.Size(94, 23);
            this.btnSelectClass.TabIndex = 408;
            this.btnSelectClass.Text = "Load Difficulty";
            this.btnSelectClass.UseVisualStyleBackColor = true;
            this.btnSelectClass.Click += new System.EventHandler(this.btnSelectClass_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Location = new System.Drawing.Point(15, 69);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(427, 328);
            this.groupBox2.TabIndex = 413;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Alien Configuration Modifiers";
            // 
            // comboBox2
            // 
            this.comboBox2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox2.FormattingEnabled = true;
            this.comboBox2.Items.AddRange(new object[] {
            "HARD",
            "MEDIUM",
            "EASY",
            "NOVICE",
            "IRON"});
            this.comboBox2.Location = new System.Drawing.Point(15, 404);
            this.comboBox2.Name = "comboBox2";
            this.comboBox2.Size = new System.Drawing.Size(327, 21);
            this.comboBox2.TabIndex = 415;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(348, 403);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(94, 23);
            this.button1.TabIndex = 414;
            this.button1.Text = "Load NPC Type";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // comboBox3
            // 
            this.comboBox3.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox3.FormattingEnabled = true;
            this.comboBox3.Items.AddRange(new object[] {
            "HARD",
            "MEDIUM",
            "EASY",
            "NOVICE",
            "IRON"});
            this.comboBox3.Location = new System.Drawing.Point(12, 870);
            this.comboBox3.Name = "comboBox3";
            this.comboBox3.Size = new System.Drawing.Size(289, 21);
            this.comboBox3.TabIndex = 417;
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(307, 869);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(135, 23);
            this.button2.TabIndex = 416;
            this.button2.Text = "Load Viewcone Set";
            this.button2.UseVisualStyleBackColor = true;
            // 
            // comboBox4
            // 
            this.comboBox4.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox4.FormattingEnabled = true;
            this.comboBox4.Items.AddRange(new object[] {
            "HARD",
            "MEDIUM",
            "EASY",
            "NOVICE",
            "IRON"});
            this.comboBox4.Location = new System.Drawing.Point(12, 897);
            this.comboBox4.Name = "comboBox4";
            this.comboBox4.Size = new System.Drawing.Size(289, 21);
            this.comboBox4.TabIndex = 419;
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(307, 896);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(135, 23);
            this.button3.TabIndex = 418;
            this.button3.Text = "Load Viewcone Type";
            this.button3.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label29);
            this.groupBox1.Controls.Add(this.label27);
            this.groupBox1.Controls.Add(this.label26);
            this.groupBox1.Controls.Add(this.label24);
            this.groupBox1.Controls.Add(this.label23);
            this.groupBox1.Controls.Add(this.max_hearing_distance_modifier);
            this.groupBox1.Controls.Add(this.label22);
            this.groupBox1.Controls.Add(this.label13);
            this.groupBox1.Controls.Add(this.label14);
            this.groupBox1.Controls.Add(this.flash_light_combined_sense_activation_modifier);
            this.groupBox1.Controls.Add(this.movement_sound_sense_activation_modifier);
            this.groupBox1.Controls.Add(this.label15);
            this.groupBox1.Controls.Add(this.flash_light_sense_activation_modifier);
            this.groupBox1.Controls.Add(this.movement_sound_combined_sense_activation_modifier);
            this.groupBox1.Controls.Add(this.label17);
            this.groupBox1.Controls.Add(this.label9);
            this.groupBox1.Controls.Add(this.label12);
            this.groupBox1.Controls.Add(this.weapon_sound_combined_sense_activation_modifier);
            this.groupBox1.Controls.Add(this.visual_sense_activation_modifier);
            this.groupBox1.Controls.Add(this.label10);
            this.groupBox1.Controls.Add(this.weapon_sound_sense_activation_modifier);
            this.groupBox1.Controls.Add(this.visual_combined_sense_activation_modifier);
            this.groupBox1.Controls.Add(this.label11);
            this.groupBox1.Location = new System.Drawing.Point(15, 432);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(427, 318);
            this.groupBox1.TabIndex = 414;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "NPC Sense Modifiers";
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.label8);
            this.groupBox3.Controls.Add(this.suspicious_item_loop_scalar);
            this.groupBox3.Controls.Add(this.label7);
            this.groupBox3.Controls.Add(this.attack_pace_modifier);
            this.groupBox3.Controls.Add(this.label6);
            this.groupBox3.Controls.Add(this.damage_received_scalar);
            this.groupBox3.Controls.Add(this.label5);
            this.groupBox3.Controls.Add(this.damage_dealt_scalar);
            this.groupBox3.Location = new System.Drawing.Point(15, 756);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(427, 108);
            this.groupBox3.TabIndex = 415;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "General NPC Modifiers";
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.label2);
            this.groupBox4.Controls.Add(this.visual_sense_stance_effect_lower_modifier);
            this.groupBox4.Controls.Add(this.label4);
            this.groupBox4.Controls.Add(this.visual_sense_stance_effect_upper_modifier);
            this.groupBox4.Controls.Add(this.label1);
            this.groupBox4.Controls.Add(this.visual_sense_exposure_effect_lower_modifier);
            this.groupBox4.Controls.Add(this.label3);
            this.groupBox4.Controls.Add(this.visual_sense_exposure_effect_upper_modifier);
            this.groupBox4.Location = new System.Drawing.Point(12, 924);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(430, 66);
            this.groupBox4.TabIndex = 416;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Viewcone Type Modifiers";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(15, 19);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(122, 13);
            this.label1.TabIndex = 420;
            this.label1.Text = "Exposure Effect Modifier";
            // 
            // visual_sense_exposure_effect_lower_modifier
            // 
            this.visual_sense_exposure_effect_lower_modifier.Location = new System.Drawing.Point(19, 35);
            this.visual_sense_exposure_effect_lower_modifier.Name = "visual_sense_exposure_effect_lower_modifier";
            this.visual_sense_exposure_effect_lower_modifier.Size = new System.Drawing.Size(49, 20);
            this.visual_sense_exposure_effect_lower_modifier.TabIndex = 421;
            // 
            // visual_sense_exposure_effect_upper_modifier
            // 
            this.visual_sense_exposure_effect_upper_modifier.Location = new System.Drawing.Point(157, 35);
            this.visual_sense_exposure_effect_upper_modifier.Name = "visual_sense_exposure_effect_upper_modifier";
            this.visual_sense_exposure_effect_upper_modifier.Size = new System.Drawing.Size(49, 20);
            this.visual_sense_exposure_effect_upper_modifier.TabIndex = 422;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(74, 38);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(77, 13);
            this.label3.TabIndex = 423;
            this.label3.Text = "Min -------> Max";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(224, 19);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(143, 13);
            this.label2.TabIndex = 424;
            this.label2.Text = "Visual Stance Effect Modifier";
            // 
            // visual_sense_stance_effect_lower_modifier
            // 
            this.visual_sense_stance_effect_lower_modifier.Location = new System.Drawing.Point(228, 35);
            this.visual_sense_stance_effect_lower_modifier.Name = "visual_sense_stance_effect_lower_modifier";
            this.visual_sense_stance_effect_lower_modifier.Size = new System.Drawing.Size(49, 20);
            this.visual_sense_stance_effect_lower_modifier.TabIndex = 425;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(283, 38);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(77, 13);
            this.label4.TabIndex = 427;
            this.label4.Text = "Min -------> Max";
            // 
            // visual_sense_stance_effect_upper_modifier
            // 
            this.visual_sense_stance_effect_upper_modifier.Location = new System.Drawing.Point(366, 35);
            this.visual_sense_stance_effect_upper_modifier.Name = "visual_sense_stance_effect_upper_modifier";
            this.visual_sense_stance_effect_upper_modifier.Size = new System.Drawing.Size(49, 20);
            this.visual_sense_stance_effect_upper_modifier.TabIndex = 426;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(12, 21);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(108, 13);
            this.label5.TabIndex = 420;
            this.label5.Text = "Damage Dealt Scalar";
            // 
            // damage_dealt_scalar
            // 
            this.damage_dealt_scalar.Location = new System.Drawing.Point(15, 37);
            this.damage_dealt_scalar.Name = "damage_dealt_scalar";
            this.damage_dealt_scalar.Size = new System.Drawing.Size(187, 20);
            this.damage_dealt_scalar.TabIndex = 421;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(12, 60);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(129, 13);
            this.label6.TabIndex = 422;
            this.label6.Text = "Damage Received Scalar";
            // 
            // damage_received_scalar
            // 
            this.damage_received_scalar.Location = new System.Drawing.Point(15, 76);
            this.damage_received_scalar.Name = "damage_received_scalar";
            this.damage_received_scalar.Size = new System.Drawing.Size(187, 20);
            this.damage_received_scalar.TabIndex = 423;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(221, 21);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(106, 13);
            this.label7.TabIndex = 424;
            this.label7.Text = "Attack Pace Modifier";
            // 
            // attack_pace_modifier
            // 
            this.attack_pace_modifier.Location = new System.Drawing.Point(224, 37);
            this.attack_pace_modifier.Name = "attack_pace_modifier";
            this.attack_pace_modifier.Size = new System.Drawing.Size(187, 20);
            this.attack_pace_modifier.TabIndex = 425;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(221, 60);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(141, 13);
            this.label8.TabIndex = 426;
            this.label8.Text = "Suspicious Item Loop Scalar";
            // 
            // suspicious_item_loop_scalar
            // 
            this.suspicious_item_loop_scalar.Location = new System.Drawing.Point(224, 76);
            this.suspicious_item_loop_scalar.Name = "suspicious_item_loop_scalar";
            this.suspicious_item_loop_scalar.Size = new System.Drawing.Size(187, 20);
            this.suspicious_item_loop_scalar.TabIndex = 427;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(221, 96);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(177, 13);
            this.label9.TabIndex = 434;
            this.label9.Text = "Combined Sense Activation Modifier";
            // 
            // weapon_sound_combined_sense_activation_modifier
            // 
            this.weapon_sound_combined_sense_activation_modifier.Location = new System.Drawing.Point(224, 112);
            this.weapon_sound_combined_sense_activation_modifier.Name = "weapon_sound_combined_sense_activation_modifier";
            this.weapon_sound_combined_sense_activation_modifier.Size = new System.Drawing.Size(187, 20);
            this.weapon_sound_combined_sense_activation_modifier.TabIndex = 435;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(221, 38);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(177, 13);
            this.label10.TabIndex = 432;
            this.label10.Text = "Combined Sense Activation Modifier";
            // 
            // visual_combined_sense_activation_modifier
            // 
            this.visual_combined_sense_activation_modifier.Location = new System.Drawing.Point(224, 54);
            this.visual_combined_sense_activation_modifier.Name = "visual_combined_sense_activation_modifier";
            this.visual_combined_sense_activation_modifier.Size = new System.Drawing.Size(187, 20);
            this.visual_combined_sense_activation_modifier.TabIndex = 433;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(12, 96);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(94, 13);
            this.label11.TabIndex = 430;
            this.label11.Text = "Activation Modifier";
            // 
            // weapon_sound_sense_activation_modifier
            // 
            this.weapon_sound_sense_activation_modifier.Location = new System.Drawing.Point(15, 112);
            this.weapon_sound_sense_activation_modifier.Name = "weapon_sound_sense_activation_modifier";
            this.weapon_sound_sense_activation_modifier.Size = new System.Drawing.Size(187, 20);
            this.weapon_sound_sense_activation_modifier.TabIndex = 431;
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(12, 38);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(94, 13);
            this.label12.TabIndex = 428;
            this.label12.Text = "Activation Modifier";
            // 
            // visual_sense_activation_modifier
            // 
            this.visual_sense_activation_modifier.Location = new System.Drawing.Point(15, 54);
            this.visual_sense_activation_modifier.Name = "visual_sense_activation_modifier";
            this.visual_sense_activation_modifier.Size = new System.Drawing.Size(187, 20);
            this.visual_sense_activation_modifier.TabIndex = 429;
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(221, 214);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(177, 13);
            this.label13.TabIndex = 442;
            this.label13.Text = "Combined Sense Activation Modifier";
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(12, 155);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(94, 13);
            this.label14.TabIndex = 436;
            this.label14.Text = "Activation Modifier";
            // 
            // flash_light_combined_sense_activation_modifier
            // 
            this.flash_light_combined_sense_activation_modifier.Location = new System.Drawing.Point(224, 230);
            this.flash_light_combined_sense_activation_modifier.Name = "flash_light_combined_sense_activation_modifier";
            this.flash_light_combined_sense_activation_modifier.Size = new System.Drawing.Size(187, 20);
            this.flash_light_combined_sense_activation_modifier.TabIndex = 443;
            // 
            // movement_sound_sense_activation_modifier
            // 
            this.movement_sound_sense_activation_modifier.Location = new System.Drawing.Point(15, 171);
            this.movement_sound_sense_activation_modifier.Name = "movement_sound_sense_activation_modifier";
            this.movement_sound_sense_activation_modifier.Size = new System.Drawing.Size(187, 20);
            this.movement_sound_sense_activation_modifier.TabIndex = 437;
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(221, 155);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(177, 13);
            this.label15.TabIndex = 440;
            this.label15.Text = "Combined Sense Activation Modifier";
            // 
            // flash_light_sense_activation_modifier
            // 
            this.flash_light_sense_activation_modifier.Location = new System.Drawing.Point(15, 230);
            this.flash_light_sense_activation_modifier.Name = "flash_light_sense_activation_modifier";
            this.flash_light_sense_activation_modifier.Size = new System.Drawing.Size(187, 20);
            this.flash_light_sense_activation_modifier.TabIndex = 439;
            // 
            // movement_sound_combined_sense_activation_modifier
            // 
            this.movement_sound_combined_sense_activation_modifier.Location = new System.Drawing.Point(224, 171);
            this.movement_sound_combined_sense_activation_modifier.Name = "movement_sound_combined_sense_activation_modifier";
            this.movement_sound_combined_sense_activation_modifier.Size = new System.Drawing.Size(187, 20);
            this.movement_sound_combined_sense_activation_modifier.TabIndex = 441;
            // 
            // label17
            // 
            this.label17.AutoSize = true;
            this.label17.Location = new System.Drawing.Point(12, 214);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(94, 13);
            this.label17.TabIndex = 438;
            this.label17.Text = "Activation Modifier";
            // 
            // max_hearing_distance_modifier
            // 
            this.max_hearing_distance_modifier.Location = new System.Drawing.Point(15, 289);
            this.max_hearing_distance_modifier.Name = "max_hearing_distance_modifier";
            this.max_hearing_distance_modifier.Size = new System.Drawing.Size(187, 20);
            this.max_hearing_distance_modifier.TabIndex = 445;
            // 
            // label22
            // 
            this.label22.AutoSize = true;
            this.label22.Location = new System.Drawing.Point(12, 273);
            this.label22.Name = "label22";
            this.label22.Size = new System.Drawing.Size(152, 13);
            this.label22.TabIndex = 444;
            this.label22.Text = "Max Hearing Distance Modifier";
            // 
            // label23
            // 
            this.label23.AutoSize = true;
            this.label23.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label23.Location = new System.Drawing.Point(6, 18);
            this.label23.Name = "label23";
            this.label23.Size = new System.Drawing.Size(102, 20);
            this.label23.TabIndex = 420;
            this.label23.Text = "Visual Sense";
            // 
            // label24
            // 
            this.label24.AutoSize = true;
            this.label24.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label24.Location = new System.Drawing.Point(6, 77);
            this.label24.Name = "label24";
            this.label24.Size = new System.Drawing.Size(170, 20);
            this.label24.TabIndex = 446;
            this.label24.Text = "Weapon Sound Sense";
            // 
            // label26
            // 
            this.label26.AutoSize = true;
            this.label26.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label26.Location = new System.Drawing.Point(6, 135);
            this.label26.Name = "label26";
            this.label26.Size = new System.Drawing.Size(184, 20);
            this.label26.TabIndex = 447;
            this.label26.Text = "Movement Sound Sense";
            // 
            // label27
            // 
            this.label27.AutoSize = true;
            this.label27.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label27.Location = new System.Drawing.Point(6, 194);
            this.label27.Name = "label27";
            this.label27.Size = new System.Drawing.Size(127, 20);
            this.label27.TabIndex = 448;
            this.label27.Text = "Flashlight Sense";
            // 
            // label29
            // 
            this.label29.AutoSize = true;
            this.label29.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label29.Location = new System.Drawing.Point(6, 253);
            this.label29.Name = "label29";
            this.label29.Size = new System.Drawing.Size(91, 20);
            this.label29.TabIndex = 449;
            this.label29.Text = "Misc Sense";
            // 
            // DifficultyEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(454, 1040);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.comboBox4);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.comboBox3);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.comboBox2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.label78);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.classSelection);
            this.Controls.Add(this.btnSelectClass);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "DifficultyEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Alien: Isolation Diffculty Editor";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label label78;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.ComboBox classSelection;
        private System.Windows.Forms.Button btnSelectClass;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.ComboBox comboBox2;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.ComboBox comboBox3;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.ComboBox comboBox4;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox visual_sense_stance_effect_lower_modifier;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox visual_sense_stance_effect_upper_modifier;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox visual_sense_exposure_effect_lower_modifier;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox visual_sense_exposure_effect_upper_modifier;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox suspicious_item_loop_scalar;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox attack_pace_modifier;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox damage_received_scalar;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox damage_dealt_scalar;
        private System.Windows.Forms.Label label29;
        private System.Windows.Forms.Label label27;
        private System.Windows.Forms.Label label26;
        private System.Windows.Forms.Label label24;
        private System.Windows.Forms.Label label23;
        private System.Windows.Forms.TextBox max_hearing_distance_modifier;
        private System.Windows.Forms.Label label22;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.TextBox flash_light_combined_sense_activation_modifier;
        private System.Windows.Forms.TextBox movement_sound_sense_activation_modifier;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.TextBox flash_light_sense_activation_modifier;
        private System.Windows.Forms.TextBox movement_sound_combined_sense_activation_modifier;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.TextBox weapon_sound_combined_sense_activation_modifier;
        private System.Windows.Forms.TextBox visual_sense_activation_modifier;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox weapon_sound_sense_activation_modifier;
        private System.Windows.Forms.TextBox visual_combined_sense_activation_modifier;
        private System.Windows.Forms.Label label11;
    }
}
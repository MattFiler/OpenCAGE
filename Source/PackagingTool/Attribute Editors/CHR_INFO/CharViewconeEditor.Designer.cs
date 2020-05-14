namespace PackagingTool
{
    partial class CharViewconeEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CharViewconeEditor));
            this.loadType = new System.Windows.Forms.Button();
            this.senseType = new System.Windows.Forms.ComboBox();
            this.label78 = new System.Windows.Forms.Label();
            this.btnSave = new System.Windows.Forms.Button();
            this.senseSets = new System.Windows.Forms.ComboBox();
            this.loadSet = new System.Windows.Forms.Button();
            this.characters = new System.Windows.Forms.ComboBox();
            this.loadChar = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label20 = new System.Windows.Forms.Label();
            this.max_damage_distance_scale_to_set_1_normal = new System.Windows.Forms.TextBox();
            this.label19 = new System.Windows.Forms.Label();
            this.max_hearing_distance_set_1_normal = new System.Windows.Forms.TextBox();
            this.label18 = new System.Windows.Forms.Label();
            this.description_set_1_normal = new System.Windows.Forms.TextBox();
            this.viewcone_set_set_1_normal = new System.Windows.Forms.ComboBox();
            this.label17 = new System.Windows.Forms.Label();
            this.senseBox = new System.Windows.Forms.GroupBox();
            this.label8 = new System.Windows.Forms.Label();
            this.positional_accuracy_scalar = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.last_sensed_expire_time = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.min_activated_time = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.decay_per_second = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.trace_threshold = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.activation_threshold = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.lower_threshold = new System.Windows.Forms.TextBox();
            this.upper_threshold = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label21 = new System.Windows.Forms.Label();
            this.combined_sense_activation_scalar = new System.Windows.Forms.TextBox();
            this.label22 = new System.Windows.Forms.Label();
            this.combined_sense_min_raw_activation = new System.Windows.Forms.TextBox();
            this.combined_sense_max_raw_activation = new System.Windows.Forms.TextBox();
            this.label26 = new System.Windows.Forms.Label();
            this.label24 = new System.Windows.Forms.Label();
            this.activation_scalar = new System.Windows.Forms.TextBox();
            this.label23 = new System.Windows.Forms.Label();
            this.min_raw_activation = new System.Windows.Forms.TextBox();
            this.max_raw_activation = new System.Windows.Forms.TextBox();
            this.label25 = new System.Windows.Forms.Label();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.Template_Name = new System.Windows.Forms.ComboBox();
            this.label10 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.squad_sense_activation_delay = new System.Windows.Forms.TextBox();
            this.groupBox2.SuspendLayout();
            this.senseBox.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // loadType
            // 
            this.loadType.Enabled = false;
            this.loadType.Location = new System.Drawing.Point(311, 288);
            this.loadType.Name = "loadType";
            this.loadType.Size = new System.Drawing.Size(125, 23);
            this.loadType.TabIndex = 475;
            this.loadType.Text = "Load Sense Type";
            this.loadType.UseVisualStyleBackColor = true;
            this.loadType.Click += new System.EventHandler(this.loadType_Click);
            // 
            // senseType
            // 
            this.senseType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.senseType.Enabled = false;
            this.senseType.FormattingEnabled = true;
            this.senseType.Items.AddRange(new object[] {
            "PLACEHOLDER"});
            this.senseType.Location = new System.Drawing.Point(17, 289);
            this.senseType.Name = "senseType";
            this.senseType.Size = new System.Drawing.Size(288, 21);
            this.senseType.TabIndex = 474;
            // 
            // label78
            // 
            this.label78.AutoSize = true;
            this.label78.Font = new System.Drawing.Font("Microsoft Sans Serif", 17F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label78.Location = new System.Drawing.Point(62, 9);
            this.label78.Name = "label78";
            this.label78.Size = new System.Drawing.Size(314, 29);
            this.label78.TabIndex = 473;
            this.label78.Text = "Alien: Isolation Sense Editor";
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(229, 592);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(207, 35);
            this.btnSave.TabIndex = 472;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // senseSets
            // 
            this.senseSets.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.senseSets.Enabled = false;
            this.senseSets.FormattingEnabled = true;
            this.senseSets.Items.AddRange(new object[] {
            "PLACEHOLDER"});
            this.senseSets.Location = new System.Drawing.Point(17, 147);
            this.senseSets.Name = "senseSets";
            this.senseSets.Size = new System.Drawing.Size(288, 21);
            this.senseSets.TabIndex = 471;
            // 
            // loadSet
            // 
            this.loadSet.Enabled = false;
            this.loadSet.Location = new System.Drawing.Point(311, 146);
            this.loadSet.Name = "loadSet";
            this.loadSet.Size = new System.Drawing.Size(125, 23);
            this.loadSet.TabIndex = 470;
            this.loadSet.Text = "Load Sense Set";
            this.loadSet.UseVisualStyleBackColor = true;
            this.loadSet.Click += new System.EventHandler(this.loadSet_Click);
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
            this.characters.Location = new System.Drawing.Point(17, 44);
            this.characters.Name = "characters";
            this.characters.Size = new System.Drawing.Size(288, 21);
            this.characters.TabIndex = 507;
            // 
            // loadChar
            // 
            this.loadChar.Location = new System.Drawing.Point(311, 43);
            this.loadChar.Name = "loadChar";
            this.loadChar.Size = new System.Drawing.Size(125, 23);
            this.loadChar.TabIndex = 506;
            this.loadChar.Text = "Load Character";
            this.loadChar.UseVisualStyleBackColor = true;
            this.loadChar.Click += new System.EventHandler(this.loadChar_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.label20);
            this.groupBox2.Controls.Add(this.max_damage_distance_scale_to_set_1_normal);
            this.groupBox2.Controls.Add(this.label19);
            this.groupBox2.Controls.Add(this.max_hearing_distance_set_1_normal);
            this.groupBox2.Controls.Add(this.label18);
            this.groupBox2.Controls.Add(this.description_set_1_normal);
            this.groupBox2.Controls.Add(this.viewcone_set_set_1_normal);
            this.groupBox2.Controls.Add(this.label17);
            this.groupBox2.Location = new System.Drawing.Point(17, 174);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(419, 109);
            this.groupBox2.TabIndex = 508;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Generic Sense Set Settings";
            // 
            // label20
            // 
            this.label20.AutoSize = true;
            this.label20.Location = new System.Drawing.Point(214, 62);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(169, 13);
            this.label20.TabIndex = 513;
            this.label20.Text = "Maximum Damage Distance Scale";
            // 
            // max_damage_distance_scale_to_set_1_normal
            // 
            this.max_damage_distance_scale_to_set_1_normal.Enabled = false;
            this.max_damage_distance_scale_to_set_1_normal.Location = new System.Drawing.Point(217, 78);
            this.max_damage_distance_scale_to_set_1_normal.Name = "max_damage_distance_scale_to_set_1_normal";
            this.max_damage_distance_scale_to_set_1_normal.Size = new System.Drawing.Size(187, 20);
            this.max_damage_distance_scale_to_set_1_normal.TabIndex = 514;
            // 
            // label19
            // 
            this.label19.AutoSize = true;
            this.label19.Location = new System.Drawing.Point(12, 62);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(136, 13);
            this.label19.TabIndex = 511;
            this.label19.Text = "Maximum Hearing Distance";
            // 
            // max_hearing_distance_set_1_normal
            // 
            this.max_hearing_distance_set_1_normal.Enabled = false;
            this.max_hearing_distance_set_1_normal.Location = new System.Drawing.Point(15, 78);
            this.max_hearing_distance_set_1_normal.Name = "max_hearing_distance_set_1_normal";
            this.max_hearing_distance_set_1_normal.Size = new System.Drawing.Size(187, 20);
            this.max_hearing_distance_set_1_normal.TabIndex = 512;
            // 
            // label18
            // 
            this.label18.AutoSize = true;
            this.label18.Location = new System.Drawing.Point(12, 22);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(79, 13);
            this.label18.TabIndex = 509;
            this.label18.Text = "Set Description";
            // 
            // description_set_1_normal
            // 
            this.description_set_1_normal.Enabled = false;
            this.description_set_1_normal.Location = new System.Drawing.Point(15, 38);
            this.description_set_1_normal.Name = "description_set_1_normal";
            this.description_set_1_normal.Size = new System.Drawing.Size(187, 20);
            this.description_set_1_normal.TabIndex = 510;
            // 
            // viewcone_set_set_1_normal
            // 
            this.viewcone_set_set_1_normal.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.viewcone_set_set_1_normal.Enabled = false;
            this.viewcone_set_set_1_normal.FormattingEnabled = true;
            this.viewcone_set_set_1_normal.Items.AddRange(new object[] {
            "VIEWCONESET_STANDARD",
            "VIEWCONESET_HUMAN",
            "VIEWCONESET_SLEEPING",
            "VIEWCONESET_ANDROID",
            "VIEWCONESET_HUMAN_HEIGHTENED"});
            this.viewcone_set_set_1_normal.Location = new System.Drawing.Point(217, 36);
            this.viewcone_set_set_1_normal.Name = "viewcone_set_set_1_normal";
            this.viewcone_set_set_1_normal.Size = new System.Drawing.Size(187, 21);
            this.viewcone_set_set_1_normal.TabIndex = 510;
            // 
            // label17
            // 
            this.label17.AutoSize = true;
            this.label17.Location = new System.Drawing.Point(214, 21);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(73, 13);
            this.label17.TabIndex = 509;
            this.label17.Text = "Viewcone Set";
            // 
            // senseBox
            // 
            this.senseBox.Controls.Add(this.label8);
            this.senseBox.Controls.Add(this.positional_accuracy_scalar);
            this.senseBox.Controls.Add(this.label7);
            this.senseBox.Controls.Add(this.last_sensed_expire_time);
            this.senseBox.Controls.Add(this.label6);
            this.senseBox.Controls.Add(this.min_activated_time);
            this.senseBox.Controls.Add(this.label5);
            this.senseBox.Controls.Add(this.decay_per_second);
            this.senseBox.Controls.Add(this.label4);
            this.senseBox.Controls.Add(this.trace_threshold);
            this.senseBox.Controls.Add(this.label1);
            this.senseBox.Controls.Add(this.activation_threshold);
            this.senseBox.Controls.Add(this.label2);
            this.senseBox.Controls.Add(this.lower_threshold);
            this.senseBox.Controls.Add(this.upper_threshold);
            this.senseBox.Controls.Add(this.label3);
            this.senseBox.Controls.Add(this.label21);
            this.senseBox.Controls.Add(this.combined_sense_activation_scalar);
            this.senseBox.Controls.Add(this.label22);
            this.senseBox.Controls.Add(this.combined_sense_min_raw_activation);
            this.senseBox.Controls.Add(this.combined_sense_max_raw_activation);
            this.senseBox.Controls.Add(this.label26);
            this.senseBox.Controls.Add(this.label24);
            this.senseBox.Controls.Add(this.activation_scalar);
            this.senseBox.Controls.Add(this.label23);
            this.senseBox.Controls.Add(this.min_raw_activation);
            this.senseBox.Controls.Add(this.max_raw_activation);
            this.senseBox.Controls.Add(this.label25);
            this.senseBox.Location = new System.Drawing.Point(17, 317);
            this.senseBox.Name = "senseBox";
            this.senseBox.Size = new System.Drawing.Size(419, 269);
            this.senseBox.TabIndex = 515;
            this.senseBox.TabStop = false;
            this.senseBox.Text = "Sense Settings";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(14, 225);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(133, 13);
            this.label8.TabIndex = 541;
            this.label8.Text = "Positional Accuracy Scalar";
            // 
            // positional_accuracy_scalar
            // 
            this.positional_accuracy_scalar.Enabled = false;
            this.positional_accuracy_scalar.Location = new System.Drawing.Point(16, 241);
            this.positional_accuracy_scalar.Name = "positional_accuracy_scalar";
            this.positional_accuracy_scalar.Size = new System.Drawing.Size(187, 20);
            this.positional_accuracy_scalar.TabIndex = 542;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(216, 185);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(124, 13);
            this.label7.TabIndex = 539;
            this.label7.Text = "Last Sensed Expire Time";
            // 
            // last_sensed_expire_time
            // 
            this.last_sensed_expire_time.Enabled = false;
            this.last_sensed_expire_time.Location = new System.Drawing.Point(219, 201);
            this.last_sensed_expire_time.Name = "last_sensed_expire_time";
            this.last_sensed_expire_time.Size = new System.Drawing.Size(187, 20);
            this.last_sensed_expire_time.TabIndex = 540;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(14, 185);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(98, 13);
            this.label6.TabIndex = 537;
            this.label6.Text = "Min Activated Time";
            // 
            // min_activated_time
            // 
            this.min_activated_time.Enabled = false;
            this.min_activated_time.Location = new System.Drawing.Point(17, 201);
            this.min_activated_time.Name = "min_activated_time";
            this.min_activated_time.Size = new System.Drawing.Size(187, 20);
            this.min_activated_time.TabIndex = 538;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(14, 144);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(97, 13);
            this.label5.TabIndex = 535;
            this.label5.Text = "Decay Per Second";
            // 
            // decay_per_second
            // 
            this.decay_per_second.Enabled = false;
            this.decay_per_second.Location = new System.Drawing.Point(17, 160);
            this.decay_per_second.Name = "decay_per_second";
            this.decay_per_second.Size = new System.Drawing.Size(187, 20);
            this.decay_per_second.TabIndex = 536;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(216, 144);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(85, 13);
            this.label4.TabIndex = 533;
            this.label4.Text = "Trace Threshold";
            // 
            // trace_threshold
            // 
            this.trace_threshold.Enabled = false;
            this.trace_threshold.Location = new System.Drawing.Point(219, 160);
            this.trace_threshold.Name = "trace_threshold";
            this.trace_threshold.Size = new System.Drawing.Size(187, 20);
            this.trace_threshold.TabIndex = 534;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(215, 103);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(104, 13);
            this.label1.TabIndex = 531;
            this.label1.Text = "Activation Threshold";
            // 
            // activation_threshold
            // 
            this.activation_threshold.Enabled = false;
            this.activation_threshold.Location = new System.Drawing.Point(218, 119);
            this.activation_threshold.Name = "activation_threshold";
            this.activation_threshold.Size = new System.Drawing.Size(187, 20);
            this.activation_threshold.TabIndex = 532;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 103);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(54, 13);
            this.label2.TabIndex = 527;
            this.label2.Text = "Threshold";
            // 
            // lower_threshold
            // 
            this.lower_threshold.Enabled = false;
            this.lower_threshold.Location = new System.Drawing.Point(17, 119);
            this.lower_threshold.Name = "lower_threshold";
            this.lower_threshold.Size = new System.Drawing.Size(49, 20);
            this.lower_threshold.TabIndex = 528;
            // 
            // upper_threshold
            // 
            this.upper_threshold.Enabled = false;
            this.upper_threshold.Location = new System.Drawing.Point(154, 119);
            this.upper_threshold.Name = "upper_threshold";
            this.upper_threshold.Size = new System.Drawing.Size(49, 20);
            this.upper_threshold.TabIndex = 529;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(69, 122);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(83, 13);
            this.label3.TabIndex = 530;
            this.label3.Text = "Lower --> Upper";
            // 
            // label21
            // 
            this.label21.AutoSize = true;
            this.label21.Location = new System.Drawing.Point(214, 62);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(170, 13);
            this.label21.TabIndex = 525;
            this.label21.Text = "Combined Sense Activation Scalar";
            // 
            // combined_sense_activation_scalar
            // 
            this.combined_sense_activation_scalar.Enabled = false;
            this.combined_sense_activation_scalar.Location = new System.Drawing.Point(217, 78);
            this.combined_sense_activation_scalar.Name = "combined_sense_activation_scalar";
            this.combined_sense_activation_scalar.Size = new System.Drawing.Size(187, 20);
            this.combined_sense_activation_scalar.TabIndex = 526;
            // 
            // label22
            // 
            this.label22.AutoSize = true;
            this.label22.Location = new System.Drawing.Point(12, 62);
            this.label22.Name = "label22";
            this.label22.Size = new System.Drawing.Size(162, 13);
            this.label22.TabIndex = 521;
            this.label22.Text = "Combined Sense Raw Activation";
            // 
            // combined_sense_min_raw_activation
            // 
            this.combined_sense_min_raw_activation.Enabled = false;
            this.combined_sense_min_raw_activation.Location = new System.Drawing.Point(16, 78);
            this.combined_sense_min_raw_activation.Name = "combined_sense_min_raw_activation";
            this.combined_sense_min_raw_activation.Size = new System.Drawing.Size(49, 20);
            this.combined_sense_min_raw_activation.TabIndex = 522;
            // 
            // combined_sense_max_raw_activation
            // 
            this.combined_sense_max_raw_activation.Enabled = false;
            this.combined_sense_max_raw_activation.Location = new System.Drawing.Point(153, 78);
            this.combined_sense_max_raw_activation.Name = "combined_sense_max_raw_activation";
            this.combined_sense_max_raw_activation.Size = new System.Drawing.Size(49, 20);
            this.combined_sense_max_raw_activation.TabIndex = 523;
            // 
            // label26
            // 
            this.label26.AutoSize = true;
            this.label26.Location = new System.Drawing.Point(71, 81);
            this.label26.Name = "label26";
            this.label26.Size = new System.Drawing.Size(77, 13);
            this.label26.TabIndex = 524;
            this.label26.Text = "Min -------> Max";
            // 
            // label24
            // 
            this.label24.AutoSize = true;
            this.label24.Location = new System.Drawing.Point(214, 21);
            this.label24.Name = "label24";
            this.label24.Size = new System.Drawing.Size(87, 13);
            this.label24.TabIndex = 519;
            this.label24.Text = "Activation Scalar";
            // 
            // activation_scalar
            // 
            this.activation_scalar.Enabled = false;
            this.activation_scalar.Location = new System.Drawing.Point(217, 37);
            this.activation_scalar.Name = "activation_scalar";
            this.activation_scalar.Size = new System.Drawing.Size(187, 20);
            this.activation_scalar.TabIndex = 520;
            // 
            // label23
            // 
            this.label23.AutoSize = true;
            this.label23.Location = new System.Drawing.Point(12, 21);
            this.label23.Name = "label23";
            this.label23.Size = new System.Drawing.Size(79, 13);
            this.label23.TabIndex = 515;
            this.label23.Text = "Raw Activation";
            // 
            // min_raw_activation
            // 
            this.min_raw_activation.Enabled = false;
            this.min_raw_activation.Location = new System.Drawing.Point(16, 37);
            this.min_raw_activation.Name = "min_raw_activation";
            this.min_raw_activation.Size = new System.Drawing.Size(49, 20);
            this.min_raw_activation.TabIndex = 516;
            // 
            // max_raw_activation
            // 
            this.max_raw_activation.Enabled = false;
            this.max_raw_activation.Location = new System.Drawing.Point(153, 37);
            this.max_raw_activation.Name = "max_raw_activation";
            this.max_raw_activation.Size = new System.Drawing.Size(49, 20);
            this.max_raw_activation.TabIndex = 517;
            // 
            // label25
            // 
            this.label25.AutoSize = true;
            this.label25.Location = new System.Drawing.Point(71, 40);
            this.label25.Name = "label25";
            this.label25.Size = new System.Drawing.Size(77, 13);
            this.label25.TabIndex = 518;
            this.label25.Text = "Min -------> Max";
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.Template_Name);
            this.groupBox3.Controls.Add(this.label10);
            this.groupBox3.Controls.Add(this.label9);
            this.groupBox3.Controls.Add(this.squad_sense_activation_delay);
            this.groupBox3.Location = new System.Drawing.Point(17, 71);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(419, 70);
            this.groupBox3.TabIndex = 515;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Generic Sense Set Settings";
            // 
            // Template_Name
            // 
            this.Template_Name.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.Template_Name.Enabled = false;
            this.Template_Name.FormattingEnabled = true;
            this.Template_Name.Items.AddRange(new object[] {
            "DEFAULTS",
            "ANDROID",
            "CIVILIAN",
            "SECURITY_GUARD"});
            this.Template_Name.Location = new System.Drawing.Point(217, 37);
            this.Template_Name.Name = "Template_Name";
            this.Template_Name.Size = new System.Drawing.Size(187, 21);
            this.Template_Name.TabIndex = 516;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(12, 22);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(151, 13);
            this.label10.TabIndex = 509;
            this.label10.Text = "Squad Sense Activation Delay";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(214, 22);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(100, 13);
            this.label9.TabIndex = 515;
            this.label9.Text = "Character Template";
            // 
            // squad_sense_activation_delay
            // 
            this.squad_sense_activation_delay.Enabled = false;
            this.squad_sense_activation_delay.Location = new System.Drawing.Point(15, 38);
            this.squad_sense_activation_delay.Name = "squad_sense_activation_delay";
            this.squad_sense_activation_delay.Size = new System.Drawing.Size(187, 20);
            this.squad_sense_activation_delay.TabIndex = 510;
            // 
            // CharViewconeEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(452, 638);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.senseBox);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.characters);
            this.Controls.Add(this.loadChar);
            this.Controls.Add(this.loadType);
            this.Controls.Add(this.senseType);
            this.Controls.Add(this.label78);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.senseSets);
            this.Controls.Add(this.loadSet);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "CharViewconeEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Alien: Isolation Sense Editor";
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.senseBox.ResumeLayout(false);
            this.senseBox.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button loadType;
        private System.Windows.Forms.ComboBox senseType;
        private System.Windows.Forms.Label label78;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.ComboBox senseSets;
        private System.Windows.Forms.Button loadSet;
        private System.Windows.Forms.ComboBox characters;
        private System.Windows.Forms.Button loadChar;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.TextBox max_damage_distance_scale_to_set_1_normal;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.TextBox max_hearing_distance_set_1_normal;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.TextBox description_set_1_normal;
        private System.Windows.Forms.ComboBox viewcone_set_set_1_normal;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.GroupBox senseBox;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox last_sensed_expire_time;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox min_activated_time;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox decay_per_second;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox trace_threshold;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox activation_threshold;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox lower_threshold;
        private System.Windows.Forms.TextBox upper_threshold;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.TextBox combined_sense_activation_scalar;
        private System.Windows.Forms.Label label22;
        private System.Windows.Forms.TextBox combined_sense_min_raw_activation;
        private System.Windows.Forms.TextBox combined_sense_max_raw_activation;
        private System.Windows.Forms.Label label26;
        private System.Windows.Forms.Label label24;
        private System.Windows.Forms.TextBox activation_scalar;
        private System.Windows.Forms.Label label23;
        private System.Windows.Forms.TextBox min_raw_activation;
        private System.Windows.Forms.TextBox max_raw_activation;
        private System.Windows.Forms.Label label25;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox squad_sense_activation_delay;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox positional_accuracy_scalar;
        private System.Windows.Forms.ComboBox Template_Name;
        private System.Windows.Forms.Label label9;
    }
}
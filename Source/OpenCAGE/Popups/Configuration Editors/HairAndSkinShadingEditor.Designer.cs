namespace OpenCAGE.ConfigEditors
{
    partial class HairAndSkinShadingEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HairAndSkinShadingEditor));
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.scattering_radius = new System.Windows.Forms.NumericUpDown();
            this.label35 = new System.Windows.Forms.Label();
            this.scattering_saturation = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.softening_length = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.alpha_threshold = new System.Windows.Forms.NumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.softening_normal_bias = new System.Windows.Forms.NumericUpDown();
            this.label4 = new System.Windows.Forms.Label();
            this.alpha_threshold_shadow = new System.Windows.Forms.NumericUpDown();
            this.label5 = new System.Windows.Forms.Label();
            this.secondary_spec_width = new System.Windows.Forms.NumericUpDown();
            this.label6 = new System.Windows.Forms.Label();
            this.secondary_spec_level = new System.Windows.Forms.NumericUpDown();
            this.label7 = new System.Windows.Forms.Label();
            this.primary_spec_width = new System.Windows.Forms.NumericUpDown();
            this.label8 = new System.Windows.Forms.Label();
            this.primary_spec_level = new System.Windows.Forms.NumericUpDown();
            this.label9 = new System.Windows.Forms.Label();
            this.softening_distance_rate = new System.Windows.Forms.NumericUpDown();
            this.label10 = new System.Windows.Forms.Label();
            this.spec_separation = new System.Windows.Forms.NumericUpDown();
            this.label11 = new System.Windows.Forms.Label();
            this.specular_ao = new System.Windows.Forms.NumericUpDown();
            this.label12 = new System.Windows.Forms.Label();
            this.diffuse_level = new System.Windows.Forms.NumericUpDown();
            this.label13 = new System.Windows.Forms.Label();
            this.specular_occlusion = new System.Windows.Forms.NumericUpDown();
            this.label14 = new System.Windows.Forms.Label();
            this.scatter_dist_rate = new System.Windows.Forms.NumericUpDown();
            this.label15 = new System.Windows.Forms.Label();
            this.occlusion_ao_infl = new System.Windows.Forms.NumericUpDown();
            this.label16 = new System.Windows.Forms.Label();
            this.ao_absorption = new System.Windows.Forms.NumericUpDown();
            this.label17 = new System.Windows.Forms.Label();
            this.occlusion_bias = new System.Windows.Forms.NumericUpDown();
            this.label18 = new System.Windows.Forms.Label();
            this.absorption_rate = new System.Windows.Forms.NumericUpDown();
            this.label19 = new System.Windows.Forms.Label();
            this.occlusion_rate = new System.Windows.Forms.NumericUpDown();
            this.label20 = new System.Windows.Forms.Label();
            this.base_absorption = new System.Windows.Forms.NumericUpDown();
            this.label21 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.scattering_radius)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.scattering_saturation)).BeginInit();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.softening_length)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.alpha_threshold)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.softening_normal_bias)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.alpha_threshold_shadow)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.secondary_spec_width)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.secondary_spec_level)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.primary_spec_width)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.primary_spec_level)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.softening_distance_rate)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.spec_separation)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.specular_ao)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.diffuse_level)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.specular_occlusion)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.scatter_dist_rate)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.occlusion_ao_infl)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ao_absorption)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.occlusion_bias)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.absorption_rate)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.occlusion_rate)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.base_absorption)).BeginInit();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.scattering_saturation);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.scattering_radius);
            this.groupBox1.Controls.Add(this.label35);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(797, 73);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Hair";
            // 
            // scattering_radius
            // 
            this.scattering_radius.DecimalPlaces = 6;
            this.scattering_radius.Location = new System.Drawing.Point(17, 38);
            this.scattering_radius.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.scattering_radius.Name = "scattering_radius";
            this.scattering_radius.Size = new System.Drawing.Size(187, 20);
            this.scattering_radius.TabIndex = 440;
            this.scattering_radius.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label35
            // 
            this.label35.AutoSize = true;
            this.label35.Location = new System.Drawing.Point(14, 22);
            this.label35.Name = "label35";
            this.label35.Size = new System.Drawing.Size(91, 13);
            this.label35.TabIndex = 439;
            this.label35.Text = "Scattering Radius";
            // 
            // scattering_saturation
            // 
            this.scattering_saturation.DecimalPlaces = 6;
            this.scattering_saturation.Location = new System.Drawing.Point(210, 38);
            this.scattering_saturation.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.scattering_saturation.Name = "scattering_saturation";
            this.scattering_saturation.Size = new System.Drawing.Size(187, 20);
            this.scattering_saturation.TabIndex = 442;
            this.scattering_saturation.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(207, 22);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(106, 13);
            this.label1.TabIndex = 441;
            this.label1.Text = "Scattering Saturation";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.specular_ao);
            this.groupBox2.Controls.Add(this.label12);
            this.groupBox2.Controls.Add(this.diffuse_level);
            this.groupBox2.Controls.Add(this.label13);
            this.groupBox2.Controls.Add(this.spec_separation);
            this.groupBox2.Controls.Add(this.label11);
            this.groupBox2.Controls.Add(this.softening_distance_rate);
            this.groupBox2.Controls.Add(this.label10);
            this.groupBox2.Controls.Add(this.specular_occlusion);
            this.groupBox2.Controls.Add(this.label14);
            this.groupBox2.Controls.Add(this.scatter_dist_rate);
            this.groupBox2.Controls.Add(this.label15);
            this.groupBox2.Controls.Add(this.occlusion_ao_infl);
            this.groupBox2.Controls.Add(this.label16);
            this.groupBox2.Controls.Add(this.ao_absorption);
            this.groupBox2.Controls.Add(this.label17);
            this.groupBox2.Controls.Add(this.occlusion_bias);
            this.groupBox2.Controls.Add(this.label18);
            this.groupBox2.Controls.Add(this.absorption_rate);
            this.groupBox2.Controls.Add(this.label19);
            this.groupBox2.Controls.Add(this.occlusion_rate);
            this.groupBox2.Controls.Add(this.label20);
            this.groupBox2.Controls.Add(this.base_absorption);
            this.groupBox2.Controls.Add(this.label21);
            this.groupBox2.Controls.Add(this.secondary_spec_width);
            this.groupBox2.Controls.Add(this.label6);
            this.groupBox2.Controls.Add(this.secondary_spec_level);
            this.groupBox2.Controls.Add(this.label7);
            this.groupBox2.Controls.Add(this.primary_spec_width);
            this.groupBox2.Controls.Add(this.label8);
            this.groupBox2.Controls.Add(this.primary_spec_level);
            this.groupBox2.Controls.Add(this.label9);
            this.groupBox2.Controls.Add(this.softening_normal_bias);
            this.groupBox2.Controls.Add(this.label4);
            this.groupBox2.Controls.Add(this.alpha_threshold_shadow);
            this.groupBox2.Controls.Add(this.label5);
            this.groupBox2.Controls.Add(this.softening_length);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.alpha_threshold);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Location = new System.Drawing.Point(12, 91);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(797, 234);
            this.groupBox2.TabIndex = 443;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Skin";
            // 
            // softening_length
            // 
            this.softening_length.DecimalPlaces = 6;
            this.softening_length.Location = new System.Drawing.Point(210, 38);
            this.softening_length.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.softening_length.Name = "softening_length";
            this.softening_length.Size = new System.Drawing.Size(187, 20);
            this.softening_length.TabIndex = 442;
            this.softening_length.Value = new decimal(new int[] {
            109,
            0,
            0,
            262144});
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(207, 22);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(88, 13);
            this.label2.TabIndex = 441;
            this.label2.Text = "Softening Length";
            // 
            // alpha_threshold
            // 
            this.alpha_threshold.DecimalPlaces = 6;
            this.alpha_threshold.Location = new System.Drawing.Point(17, 38);
            this.alpha_threshold.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.alpha_threshold.Name = "alpha_threshold";
            this.alpha_threshold.Size = new System.Drawing.Size(187, 20);
            this.alpha_threshold.TabIndex = 440;
            this.alpha_threshold.Value = new decimal(new int[] {
            26667,
            0,
            0,
            393216});
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(14, 22);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(84, 13);
            this.label3.TabIndex = 439;
            this.label3.Text = "Alpha Threshold";
            // 
            // softening_normal_bias
            // 
            this.softening_normal_bias.DecimalPlaces = 6;
            this.softening_normal_bias.Location = new System.Drawing.Point(210, 78);
            this.softening_normal_bias.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.softening_normal_bias.Name = "softening_normal_bias";
            this.softening_normal_bias.Size = new System.Drawing.Size(187, 20);
            this.softening_normal_bias.TabIndex = 446;
            this.softening_normal_bias.Value = new decimal(new int[] {
            98333,
            0,
            0,
            393216});
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(207, 62);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(111, 13);
            this.label4.TabIndex = 445;
            this.label4.Text = "Softening Normal Bias";
            // 
            // alpha_threshold_shadow
            // 
            this.alpha_threshold_shadow.DecimalPlaces = 6;
            this.alpha_threshold_shadow.Location = new System.Drawing.Point(17, 78);
            this.alpha_threshold_shadow.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.alpha_threshold_shadow.Name = "alpha_threshold_shadow";
            this.alpha_threshold_shadow.Size = new System.Drawing.Size(187, 20);
            this.alpha_threshold_shadow.TabIndex = 444;
            this.alpha_threshold_shadow.Value = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(14, 62);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(137, 13);
            this.label5.TabIndex = 443;
            this.label5.Text = "Alpha Threshold (Shadows)";
            // 
            // secondary_spec_width
            // 
            this.secondary_spec_width.DecimalPlaces = 6;
            this.secondary_spec_width.Location = new System.Drawing.Point(210, 197);
            this.secondary_spec_width.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.secondary_spec_width.Name = "secondary_spec_width";
            this.secondary_spec_width.Size = new System.Drawing.Size(187, 20);
            this.secondary_spec_width.TabIndex = 454;
            this.secondary_spec_width.Value = new decimal(new int[] {
            786667,
            0,
            0,
            327680});
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(207, 181);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(134, 13);
            this.label6.TabIndex = 453;
            this.label6.Text = "Secondary Specular Width";
            // 
            // secondary_spec_level
            // 
            this.secondary_spec_level.DecimalPlaces = 6;
            this.secondary_spec_level.Location = new System.Drawing.Point(17, 197);
            this.secondary_spec_level.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.secondary_spec_level.Name = "secondary_spec_level";
            this.secondary_spec_level.Size = new System.Drawing.Size(187, 20);
            this.secondary_spec_level.TabIndex = 452;
            this.secondary_spec_level.Value = new decimal(new int[] {
            10,
            0,
            0,
            65536});
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(14, 181);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(132, 13);
            this.label7.TabIndex = 451;
            this.label7.Text = "Secondary Specular Level";
            // 
            // primary_spec_width
            // 
            this.primary_spec_width.DecimalPlaces = 6;
            this.primary_spec_width.Location = new System.Drawing.Point(210, 157);
            this.primary_spec_width.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.primary_spec_width.Name = "primary_spec_width";
            this.primary_spec_width.Size = new System.Drawing.Size(187, 20);
            this.primary_spec_width.TabIndex = 450;
            this.primary_spec_width.Value = new decimal(new int[] {
            22,
            0,
            0,
            65536});
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(207, 141);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(117, 13);
            this.label8.TabIndex = 449;
            this.label8.Text = "Primary Specular Width";
            // 
            // primary_spec_level
            // 
            this.primary_spec_level.DecimalPlaces = 6;
            this.primary_spec_level.Location = new System.Drawing.Point(17, 157);
            this.primary_spec_level.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.primary_spec_level.Name = "primary_spec_level";
            this.primary_spec_level.Size = new System.Drawing.Size(187, 20);
            this.primary_spec_level.TabIndex = 448;
            this.primary_spec_level.Value = new decimal(new int[] {
            68333,
            0,
            0,
            393216});
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(14, 141);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(115, 13);
            this.label9.TabIndex = 447;
            this.label9.Text = "Primary Specular Level";
            // 
            // softening_distance_rate
            // 
            this.softening_distance_rate.DecimalPlaces = 6;
            this.softening_distance_rate.Location = new System.Drawing.Point(210, 118);
            this.softening_distance_rate.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.softening_distance_rate.Name = "softening_distance_rate";
            this.softening_distance_rate.Size = new System.Drawing.Size(187, 20);
            this.softening_distance_rate.TabIndex = 458;
            this.softening_distance_rate.Value = new decimal(new int[] {
            218333,
            0,
            0,
            393216});
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(207, 102);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(123, 13);
            this.label10.TabIndex = 457;
            this.label10.Text = "Softening Distance Rate";
            // 
            // spec_separation
            // 
            this.spec_separation.DecimalPlaces = 6;
            this.spec_separation.Location = new System.Drawing.Point(17, 118);
            this.spec_separation.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.spec_separation.Name = "spec_separation";
            this.spec_separation.Size = new System.Drawing.Size(187, 20);
            this.spec_separation.TabIndex = 456;
            this.spec_separation.Value = new decimal(new int[] {
            128333,
            0,
            0,
            327680});
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(14, 102);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(103, 13);
            this.label11.TabIndex = 455;
            this.label11.Text = "Specular Separation";
            // 
            // specular_ao
            // 
            this.specular_ao.DecimalPlaces = 6;
            this.specular_ao.Location = new System.Drawing.Point(596, 197);
            this.specular_ao.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.specular_ao.Name = "specular_ao";
            this.specular_ao.Size = new System.Drawing.Size(187, 20);
            this.specular_ao.TabIndex = 478;
            this.specular_ao.Value = new decimal(new int[] {
            63,
            0,
            0,
            65536});
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(593, 181);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(67, 13);
            this.label12.TabIndex = 477;
            this.label12.Text = "Specular AO";
            // 
            // diffuse_level
            // 
            this.diffuse_level.DecimalPlaces = 6;
            this.diffuse_level.Location = new System.Drawing.Point(403, 197);
            this.diffuse_level.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.diffuse_level.Name = "diffuse_level";
            this.diffuse_level.Size = new System.Drawing.Size(187, 20);
            this.diffuse_level.TabIndex = 476;
            this.diffuse_level.Value = new decimal(new int[] {
            20,
            0,
            0,
            65536});
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(400, 181);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(69, 13);
            this.label13.TabIndex = 475;
            this.label13.Text = "Diffuse Level";
            // 
            // specular_occlusion
            // 
            this.specular_occlusion.DecimalPlaces = 6;
            this.specular_occlusion.Location = new System.Drawing.Point(596, 157);
            this.specular_occlusion.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.specular_occlusion.Name = "specular_occlusion";
            this.specular_occlusion.Size = new System.Drawing.Size(187, 20);
            this.specular_occlusion.TabIndex = 474;
            this.specular_occlusion.Value = new decimal(new int[] {
            48,
            0,
            0,
            65536});
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(593, 141);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(99, 13);
            this.label14.TabIndex = 473;
            this.label14.Text = "Specular Occlusion";
            // 
            // scatter_dist_rate
            // 
            this.scatter_dist_rate.DecimalPlaces = 6;
            this.scatter_dist_rate.Location = new System.Drawing.Point(403, 157);
            this.scatter_dist_rate.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.scatter_dist_rate.Name = "scatter_dist_rate";
            this.scatter_dist_rate.Size = new System.Drawing.Size(187, 20);
            this.scatter_dist_rate.TabIndex = 472;
            this.scatter_dist_rate.Value = new decimal(new int[] {
            20,
            0,
            0,
            65536});
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(400, 141);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(112, 13);
            this.label15.TabIndex = 471;
            this.label15.Text = "Scatter Distance Rate";
            // 
            // occlusion_ao_infl
            // 
            this.occlusion_ao_infl.DecimalPlaces = 6;
            this.occlusion_ao_infl.Location = new System.Drawing.Point(596, 117);
            this.occlusion_ao_infl.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.occlusion_ao_infl.Name = "occlusion_ao_infl";
            this.occlusion_ao_infl.Size = new System.Drawing.Size(187, 20);
            this.occlusion_ao_infl.TabIndex = 470;
            this.occlusion_ao_infl.Value = new decimal(new int[] {
            84,
            0,
            0,
            131072});
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(593, 101);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(119, 13);
            this.label16.TabIndex = 469;
            this.label16.Text = "Occlusion AO Influence";
            // 
            // ao_absorption
            // 
            this.ao_absorption.DecimalPlaces = 6;
            this.ao_absorption.Location = new System.Drawing.Point(403, 117);
            this.ao_absorption.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.ao_absorption.Name = "ao_absorption";
            this.ao_absorption.Size = new System.Drawing.Size(187, 20);
            this.ao_absorption.TabIndex = 468;
            this.ao_absorption.Value = new decimal(new int[] {
            766667,
            0,
            0,
            393216});
            // 
            // label17
            // 
            this.label17.AutoSize = true;
            this.label17.Location = new System.Drawing.Point(400, 101);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(101, 13);
            this.label17.TabIndex = 467;
            this.label17.Text = "AO Absorption Rate";
            // 
            // occlusion_bias
            // 
            this.occlusion_bias.DecimalPlaces = 6;
            this.occlusion_bias.Location = new System.Drawing.Point(596, 78);
            this.occlusion_bias.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.occlusion_bias.Name = "occlusion_bias";
            this.occlusion_bias.Size = new System.Drawing.Size(187, 20);
            this.occlusion_bias.TabIndex = 466;
            this.occlusion_bias.Value = new decimal(new int[] {
            526667,
            0,
            0,
            393216});
            // 
            // label18
            // 
            this.label18.AutoSize = true;
            this.label18.Location = new System.Drawing.Point(593, 62);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(77, 13);
            this.label18.TabIndex = 465;
            this.label18.Text = "Occlusion Bias";
            // 
            // absorption_rate
            // 
            this.absorption_rate.DecimalPlaces = 6;
            this.absorption_rate.Location = new System.Drawing.Point(403, 78);
            this.absorption_rate.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.absorption_rate.Name = "absorption_rate";
            this.absorption_rate.Size = new System.Drawing.Size(187, 20);
            this.absorption_rate.TabIndex = 464;
            this.absorption_rate.Value = new decimal(new int[] {
            150667,
            0,
            0,
            327680});
            // 
            // label19
            // 
            this.label19.AutoSize = true;
            this.label19.Location = new System.Drawing.Point(400, 62);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(83, 13);
            this.label19.TabIndex = 463;
            this.label19.Text = "Absorption Rate";
            // 
            // occlusion_rate
            // 
            this.occlusion_rate.DecimalPlaces = 6;
            this.occlusion_rate.Location = new System.Drawing.Point(596, 38);
            this.occlusion_rate.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.occlusion_rate.Name = "occlusion_rate";
            this.occlusion_rate.Size = new System.Drawing.Size(187, 20);
            this.occlusion_rate.TabIndex = 462;
            this.occlusion_rate.Value = new decimal(new int[] {
            1335,
            0,
            0,
            131072});
            // 
            // label20
            // 
            this.label20.AutoSize = true;
            this.label20.Location = new System.Drawing.Point(593, 22);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(80, 13);
            this.label20.TabIndex = 461;
            this.label20.Text = "Occlusion Rate";
            // 
            // base_absorption
            // 
            this.base_absorption.DecimalPlaces = 6;
            this.base_absorption.Location = new System.Drawing.Point(403, 38);
            this.base_absorption.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.base_absorption.Name = "base_absorption";
            this.base_absorption.Size = new System.Drawing.Size(187, 20);
            this.base_absorption.TabIndex = 460;
            this.base_absorption.Value = new decimal(new int[] {
            124,
            0,
            0,
            131072});
            // 
            // label21
            // 
            this.label21.AutoSize = true;
            this.label21.Location = new System.Drawing.Point(400, 22);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(84, 13);
            this.label21.TabIndex = 459;
            this.label21.Text = "Base Absorption";
            // 
            // HairAndSkinShadingEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(818, 336);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "HairAndSkinShadingEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Hair and Skin Shading Editor";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.scattering_radius)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.scattering_saturation)).EndInit();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.softening_length)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.alpha_threshold)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.softening_normal_bias)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.alpha_threshold_shadow)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.secondary_spec_width)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.secondary_spec_level)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.primary_spec_width)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.primary_spec_level)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.softening_distance_rate)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.spec_separation)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.specular_ao)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.diffuse_level)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.specular_occlusion)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.scatter_dist_rate)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.occlusion_ao_infl)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ao_absorption)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.occlusion_bias)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.absorption_rate)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.occlusion_rate)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.base_absorption)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.NumericUpDown scattering_saturation;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown scattering_radius;
        private System.Windows.Forms.Label label35;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.NumericUpDown softening_length;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown alpha_threshold;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown specular_ao;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.NumericUpDown diffuse_level;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.NumericUpDown specular_occlusion;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.NumericUpDown scatter_dist_rate;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.NumericUpDown occlusion_ao_infl;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.NumericUpDown ao_absorption;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.NumericUpDown occlusion_bias;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.NumericUpDown absorption_rate;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.NumericUpDown occlusion_rate;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.NumericUpDown base_absorption;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.NumericUpDown softening_distance_rate;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.NumericUpDown spec_separation;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.NumericUpDown secondary_spec_width;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.NumericUpDown secondary_spec_level;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.NumericUpDown primary_spec_width;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.NumericUpDown primary_spec_level;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.NumericUpDown softening_normal_bias;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.NumericUpDown alpha_threshold_shadow;
        private System.Windows.Forms.Label label5;
    }
}

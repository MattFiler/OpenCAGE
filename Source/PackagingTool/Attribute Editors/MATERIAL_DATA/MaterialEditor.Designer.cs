namespace Alien_Isolation_Mod_Tools.Attribute_Editors.MATERIAL_DATA
{
    partial class MaterialEditor
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
            this.materialList = new System.Windows.Forms.ListBox();
            this.loadSelectedMaterial = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.Identifier = new System.Windows.Forms.TextBox();
            this.Template_Name = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.friction = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.density = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.flammable = new System.Windows.Forms.CheckBox();
            this.is_hard_surface = new System.Windows.Forms.CheckBox();
            this.label5 = new System.Windows.Forms.Label();
            this.collision_type = new System.Windows.Forms.ComboBox();
            this.material_type = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.mechanic_type = new System.Windows.Forms.ComboBox();
            this.label7 = new System.Windows.Forms.Label();
            this.filter = new System.Windows.Forms.ComboBox();
            this.label8 = new System.Windows.Forms.Label();
            this.sound_group = new System.Windows.Forms.ComboBox();
            this.label9 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.sound_effect = new System.Windows.Forms.ComboBox();
            this.label10 = new System.Windows.Forms.Label();
            this.visual_effect = new System.Windows.Forms.ComboBox();
            this.label11 = new System.Windows.Forms.Label();
            this.shatter_force = new System.Windows.Forms.TextBox();
            this.label12 = new System.Windows.Forms.Label();
            this.ballistic_absorption = new System.Windows.Forms.TextBox();
            this.label13 = new System.Windows.Forms.Label();
            this.richochet = new System.Windows.Forms.TextBox();
            this.label14 = new System.Windows.Forms.Label();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.debris_effect = new System.Windows.Forms.ComboBox();
            this.label15 = new System.Windows.Forms.Label();
            this.impact_effect = new System.Windows.Forms.ComboBox();
            this.label16 = new System.Windows.Forms.Label();
            this.spark_effect = new System.Windows.Forms.ComboBox();
            this.label17 = new System.Windows.Forms.Label();
            this.decal_effect = new System.Windows.Forms.ComboBox();
            this.label18 = new System.Windows.Forms.Label();
            this.debris_elements_to_emit = new System.Windows.Forms.TextBox();
            this.label21 = new System.Windows.Forms.Label();
            this.label27 = new System.Windows.Forms.Label();
            this.max_linear_debris_velocity = new System.Windows.Forms.TextBox();
            this.min_linear_debris_velocity = new System.Windows.Forms.TextBox();
            this.label19 = new System.Windows.Forms.Label();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.SuspendLayout();
            // 
            // materialList
            // 
            this.materialList.FormattingEnabled = true;
            this.materialList.Location = new System.Drawing.Point(12, 12);
            this.materialList.Name = "materialList";
            this.materialList.Size = new System.Drawing.Size(319, 576);
            this.materialList.TabIndex = 1;
            // 
            // loadSelectedMaterial
            // 
            this.loadSelectedMaterial.Location = new System.Drawing.Point(12, 594);
            this.loadSelectedMaterial.Name = "loadSelectedMaterial";
            this.loadSelectedMaterial.Size = new System.Drawing.Size(319, 23);
            this.loadSelectedMaterial.TabIndex = 2;
            this.loadSelectedMaterial.Text = "Load Selected Material";
            this.loadSelectedMaterial.UseVisualStyleBackColor = true;
            this.loadSelectedMaterial.Click += new System.EventHandler(this.loadSelectedMaterial_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(225, 20);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(78, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "Identifier Name";
            // 
            // Identifier
            // 
            this.Identifier.Location = new System.Drawing.Point(228, 36);
            this.Identifier.Name = "Identifier";
            this.Identifier.ReadOnly = true;
            this.Identifier.Size = new System.Drawing.Size(213, 20);
            this.Identifier.TabIndex = 4;
            // 
            // Template_Name
            // 
            this.Template_Name.Location = new System.Drawing.Point(9, 36);
            this.Template_Name.Name = "Template_Name";
            this.Template_Name.ReadOnly = true;
            this.Template_Name.Size = new System.Drawing.Size(213, 20);
            this.Template_Name.TabIndex = 6;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 20);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(82, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "Template Name";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.sound_group);
            this.groupBox1.Controls.Add(this.label9);
            this.groupBox1.Controls.Add(this.filter);
            this.groupBox1.Controls.Add(this.label8);
            this.groupBox1.Controls.Add(this.mechanic_type);
            this.groupBox1.Controls.Add(this.label7);
            this.groupBox1.Controls.Add(this.material_type);
            this.groupBox1.Controls.Add(this.label6);
            this.groupBox1.Controls.Add(this.collision_type);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.is_hard_surface);
            this.groupBox1.Controls.Add(this.flammable);
            this.groupBox1.Controls.Add(this.density);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.friction);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Location = new System.Drawing.Point(9, 62);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(414, 170);
            this.groupBox1.TabIndex = 7;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Physics Properties";
            // 
            // friction
            // 
            this.friction.Location = new System.Drawing.Point(210, 116);
            this.friction.Name = "friction";
            this.friction.Size = new System.Drawing.Size(79, 20);
            this.friction.TabIndex = 9;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(207, 100);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(41, 13);
            this.label3.TabIndex = 8;
            this.label3.Text = "Friction";
            // 
            // density
            // 
            this.density.Location = new System.Drawing.Point(295, 116);
            this.density.Name = "density";
            this.density.Size = new System.Drawing.Size(79, 20);
            this.density.TabIndex = 11;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(292, 100);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(42, 13);
            this.label4.TabIndex = 10;
            this.label4.Text = "Density";
            // 
            // flammable
            // 
            this.flammable.AutoSize = true;
            this.flammable.Location = new System.Drawing.Point(10, 143);
            this.flammable.Name = "flammable";
            this.flammable.Size = new System.Drawing.Size(76, 17);
            this.flammable.TabIndex = 12;
            this.flammable.Text = "Flammable";
            this.flammable.UseVisualStyleBackColor = true;
            // 
            // is_hard_surface
            // 
            this.is_hard_surface.AutoSize = true;
            this.is_hard_surface.Location = new System.Drawing.Point(92, 143);
            this.is_hard_surface.Name = "is_hard_surface";
            this.is_hard_surface.Size = new System.Drawing.Size(100, 17);
            this.is_hard_surface.TabIndex = 13;
            this.is_hard_surface.Text = "Is Hard Surface";
            this.is_hard_surface.UseVisualStyleBackColor = true;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(7, 20);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(72, 13);
            this.label5.TabIndex = 14;
            this.label5.Text = "Collision Type";
            // 
            // collision_type
            // 
            this.collision_type.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.collision_type.FormattingEnabled = true;
            this.collision_type.Location = new System.Drawing.Point(10, 36);
            this.collision_type.Name = "collision_type";
            this.collision_type.Size = new System.Drawing.Size(194, 21);
            this.collision_type.TabIndex = 15;
            // 
            // material_type
            // 
            this.material_type.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.material_type.FormattingEnabled = true;
            this.material_type.Location = new System.Drawing.Point(10, 76);
            this.material_type.Name = "material_type";
            this.material_type.Size = new System.Drawing.Size(194, 21);
            this.material_type.TabIndex = 17;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(7, 60);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(71, 13);
            this.label6.TabIndex = 16;
            this.label6.Text = "Material Type";
            // 
            // mechanic_type
            // 
            this.mechanic_type.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.mechanic_type.FormattingEnabled = true;
            this.mechanic_type.Location = new System.Drawing.Point(10, 116);
            this.mechanic_type.Name = "mechanic_type";
            this.mechanic_type.Size = new System.Drawing.Size(194, 21);
            this.mechanic_type.TabIndex = 19;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(7, 100);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(81, 13);
            this.label7.TabIndex = 18;
            this.label7.Text = "Mechanic Type";
            // 
            // filter
            // 
            this.filter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.filter.FormattingEnabled = true;
            this.filter.Location = new System.Drawing.Point(210, 36);
            this.filter.Name = "filter";
            this.filter.Size = new System.Drawing.Size(194, 21);
            this.filter.TabIndex = 21;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(207, 20);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(29, 13);
            this.label8.TabIndex = 20;
            this.label8.Text = "Filter";
            // 
            // sound_group
            // 
            this.sound_group.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.sound_group.FormattingEnabled = true;
            this.sound_group.Location = new System.Drawing.Point(210, 76);
            this.sound_group.Name = "sound_group";
            this.sound_group.Size = new System.Drawing.Size(194, 21);
            this.sound_group.TabIndex = 23;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(207, 60);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(70, 13);
            this.label9.TabIndex = 22;
            this.label9.Text = "Sound Group";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.richochet);
            this.groupBox2.Controls.Add(this.label14);
            this.groupBox2.Controls.Add(this.shatter_force);
            this.groupBox2.Controls.Add(this.sound_effect);
            this.groupBox2.Controls.Add(this.label12);
            this.groupBox2.Controls.Add(this.label11);
            this.groupBox2.Controls.Add(this.ballistic_absorption);
            this.groupBox2.Controls.Add(this.label10);
            this.groupBox2.Controls.Add(this.label13);
            this.groupBox2.Controls.Add(this.visual_effect);
            this.groupBox2.Location = new System.Drawing.Point(429, 62);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(215, 170);
            this.groupBox2.TabIndex = 8;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Ballistics Properties";
            // 
            // sound_effect
            // 
            this.sound_effect.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.sound_effect.FormattingEnabled = true;
            this.sound_effect.Location = new System.Drawing.Point(9, 76);
            this.sound_effect.Name = "sound_effect";
            this.sound_effect.Size = new System.Drawing.Size(194, 21);
            this.sound_effect.TabIndex = 27;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(6, 60);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(69, 13);
            this.label10.TabIndex = 26;
            this.label10.Text = "Sound Effect";
            // 
            // visual_effect
            // 
            this.visual_effect.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.visual_effect.FormattingEnabled = true;
            this.visual_effect.Location = new System.Drawing.Point(9, 36);
            this.visual_effect.Name = "visual_effect";
            this.visual_effect.Size = new System.Drawing.Size(194, 21);
            this.visual_effect.TabIndex = 25;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(6, 20);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(66, 13);
            this.label11.TabIndex = 24;
            this.label11.Text = "Visual Effect";
            // 
            // shatter_force
            // 
            this.shatter_force.Location = new System.Drawing.Point(140, 116);
            this.shatter_force.Name = "shatter_force";
            this.shatter_force.Size = new System.Drawing.Size(56, 20);
            this.shatter_force.TabIndex = 27;
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(137, 100);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(71, 13);
            this.label12.TabIndex = 26;
            this.label12.Text = "Shatter Force";
            // 
            // ballistic_absorption
            // 
            this.ballistic_absorption.Location = new System.Drawing.Point(9, 116);
            this.ballistic_absorption.Name = "ballistic_absorption";
            this.ballistic_absorption.Size = new System.Drawing.Size(56, 20);
            this.ballistic_absorption.TabIndex = 25;
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(6, 100);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(57, 13);
            this.label13.TabIndex = 24;
            this.label13.Text = "Absorption";
            // 
            // richochet
            // 
            this.richochet.Location = new System.Drawing.Point(74, 116);
            this.richochet.Name = "richochet";
            this.richochet.Size = new System.Drawing.Size(56, 20);
            this.richochet.TabIndex = 29;
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(71, 100);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(50, 13);
            this.label14.TabIndex = 28;
            this.label14.Text = "Ricochet";
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.label19);
            this.groupBox3.Controls.Add(this.label27);
            this.groupBox3.Controls.Add(this.max_linear_debris_velocity);
            this.groupBox3.Controls.Add(this.min_linear_debris_velocity);
            this.groupBox3.Controls.Add(this.debris_elements_to_emit);
            this.groupBox3.Controls.Add(this.label21);
            this.groupBox3.Controls.Add(this.decal_effect);
            this.groupBox3.Controls.Add(this.label18);
            this.groupBox3.Controls.Add(this.spark_effect);
            this.groupBox3.Controls.Add(this.label17);
            this.groupBox3.Controls.Add(this.debris_effect);
            this.groupBox3.Controls.Add(this.label16);
            this.groupBox3.Controls.Add(this.label15);
            this.groupBox3.Controls.Add(this.impact_effect);
            this.groupBox3.Location = new System.Drawing.Point(9, 238);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(635, 349);
            this.groupBox3.TabIndex = 9;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Projectile VFX Properties";
            // 
            // debris_effect
            // 
            this.debris_effect.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.debris_effect.FormattingEnabled = true;
            this.debris_effect.Location = new System.Drawing.Point(210, 35);
            this.debris_effect.Name = "debris_effect";
            this.debris_effect.Size = new System.Drawing.Size(194, 21);
            this.debris_effect.TabIndex = 27;
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(207, 19);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(68, 13);
            this.label15.TabIndex = 26;
            this.label15.Text = "Debris Effect";
            // 
            // impact_effect
            // 
            this.impact_effect.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.impact_effect.FormattingEnabled = true;
            this.impact_effect.Location = new System.Drawing.Point(10, 35);
            this.impact_effect.Name = "impact_effect";
            this.impact_effect.Size = new System.Drawing.Size(194, 21);
            this.impact_effect.TabIndex = 25;
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(7, 19);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(70, 13);
            this.label16.TabIndex = 24;
            this.label16.Text = "Impact Effect";
            // 
            // spark_effect
            // 
            this.spark_effect.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.spark_effect.FormattingEnabled = true;
            this.spark_effect.Location = new System.Drawing.Point(10, 75);
            this.spark_effect.Name = "spark_effect";
            this.spark_effect.Size = new System.Drawing.Size(194, 21);
            this.spark_effect.TabIndex = 29;
            // 
            // label17
            // 
            this.label17.AutoSize = true;
            this.label17.Location = new System.Drawing.Point(7, 59);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(66, 13);
            this.label17.TabIndex = 28;
            this.label17.Text = "Spark Effect";
            // 
            // decal_effect
            // 
            this.decal_effect.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.decal_effect.FormattingEnabled = true;
            this.decal_effect.Location = new System.Drawing.Point(210, 75);
            this.decal_effect.Name = "decal_effect";
            this.decal_effect.Size = new System.Drawing.Size(194, 21);
            this.decal_effect.TabIndex = 31;
            // 
            // label18
            // 
            this.label18.AutoSize = true;
            this.label18.Location = new System.Drawing.Point(207, 59);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(66, 13);
            this.label18.TabIndex = 30;
            this.label18.Text = "Decal Effect";
            // 
            // debris_elements_to_emit
            // 
            this.debris_elements_to_emit.Location = new System.Drawing.Point(410, 76);
            this.debris_elements_to_emit.Name = "debris_elements_to_emit";
            this.debris_elements_to_emit.Size = new System.Drawing.Size(79, 20);
            this.debris_elements_to_emit.TabIndex = 33;
            // 
            // label21
            // 
            this.label21.AutoSize = true;
            this.label21.Location = new System.Drawing.Point(407, 60);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(118, 13);
            this.label21.TabIndex = 32;
            this.label21.Text = "Debris Elements to Emit";
            // 
            // label27
            // 
            this.label27.AutoSize = true;
            this.label27.Location = new System.Drawing.Point(465, 38);
            this.label27.Name = "label27";
            this.label27.Size = new System.Drawing.Size(77, 13);
            this.label27.TabIndex = 61;
            this.label27.Text = "Min -------> Max";
            // 
            // max_linear_debris_velocity
            // 
            this.max_linear_debris_velocity.Enabled = false;
            this.max_linear_debris_velocity.Location = new System.Drawing.Point(548, 35);
            this.max_linear_debris_velocity.Name = "max_linear_debris_velocity";
            this.max_linear_debris_velocity.Size = new System.Drawing.Size(49, 20);
            this.max_linear_debris_velocity.TabIndex = 60;
            // 
            // min_linear_debris_velocity
            // 
            this.min_linear_debris_velocity.Enabled = false;
            this.min_linear_debris_velocity.Location = new System.Drawing.Point(410, 35);
            this.min_linear_debris_velocity.Name = "min_linear_debris_velocity";
            this.min_linear_debris_velocity.Size = new System.Drawing.Size(49, 20);
            this.min_linear_debris_velocity.TabIndex = 59;
            // 
            // label19
            // 
            this.label19.AutoSize = true;
            this.label19.Location = new System.Drawing.Point(407, 19);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(109, 13);
            this.label19.TabIndex = 62;
            this.label19.Text = "Linear Debris Velocity";
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.label2);
            this.groupBox4.Controls.Add(this.groupBox3);
            this.groupBox4.Controls.Add(this.label1);
            this.groupBox4.Controls.Add(this.groupBox2);
            this.groupBox4.Controls.Add(this.Identifier);
            this.groupBox4.Controls.Add(this.groupBox1);
            this.groupBox4.Controls.Add(this.Template_Name);
            this.groupBox4.Location = new System.Drawing.Point(337, 12);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(653, 605);
            this.groupBox4.TabIndex = 10;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Material Properties";
            // 
            // MaterialEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1003, 627);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.loadSelectedMaterial);
            this.Controls.Add(this.materialList);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "MaterialEditor";
            this.Text = "Material Property Editor";
            this.Load += new System.EventHandler(this.MaterialEditor_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.ListBox materialList;
        private System.Windows.Forms.Button loadSelectedMaterial;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox Identifier;
        private System.Windows.Forms.TextBox Template_Name;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox friction;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox sound_group;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.ComboBox filter;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.ComboBox mechanic_type;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.ComboBox material_type;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ComboBox collision_type;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.CheckBox is_hard_surface;
        private System.Windows.Forms.CheckBox flammable;
        private System.Windows.Forms.TextBox density;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TextBox richochet;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.TextBox shatter_force;
        private System.Windows.Forms.ComboBox sound_effect;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.TextBox ballistic_absorption;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.ComboBox visual_effect;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.TextBox debris_elements_to_emit;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.ComboBox decal_effect;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.ComboBox spark_effect;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.ComboBox debris_effect;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.ComboBox impact_effect;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.Label label27;
        private System.Windows.Forms.TextBox max_linear_debris_velocity;
        private System.Windows.Forms.TextBox min_linear_debris_velocity;
        private System.Windows.Forms.GroupBox groupBox4;
    }
}
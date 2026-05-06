namespace OpenCAGE.ConfigEditors
{
    partial class SenseSet
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
            this.label20 = new System.Windows.Forms.Label();
            this.label19 = new System.Windows.Forms.Label();
            this.viewcone_set = new System.Windows.Forms.ComboBox();
            this.label17 = new System.Windows.Forms.Label();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.visualSense = new ConfigEditors.SenseType();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.weaponSense = new ConfigEditors.SenseType();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.movementSense = new ConfigEditors.SenseType();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.damageSense = new ConfigEditors.SenseType();
            this.tabPage5 = new System.Windows.Forms.TabPage();
            this.touchedSense = new ConfigEditors.SenseType();
            this.tabPage6 = new System.Windows.Forms.TabPage();
            this.flashlightSense = new ConfigEditors.SenseType();
            this.tabPage7 = new System.Windows.Forms.TabPage();
            this.flamethrowerSense = new ConfigEditors.SenseType();
            this.tabPage8 = new System.Windows.Forms.TabPage();
            this.combinedSense = new ConfigEditors.SenseType();
            this.max_hearing_distance = new System.Windows.Forms.NumericUpDown();
            this.max_damage_distance_scale_to = new System.Windows.Forms.NumericUpDown();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.tabPage4.SuspendLayout();
            this.tabPage5.SuspendLayout();
            this.tabPage6.SuspendLayout();
            this.tabPage7.SuspendLayout();
            this.tabPage8.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.max_hearing_distance)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.max_damage_distance_scale_to)).BeginInit();
            this.SuspendLayout();
            // 
            // label20
            // 
            this.label20.AutoSize = true;
            this.label20.Location = new System.Drawing.Point(13, 49);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(169, 13);
            this.label20.TabIndex = 533;
            this.label20.Text = "Maximum Damage Distance Scale";
            // 
            // label19
            // 
            this.label19.AutoSize = true;
            this.label19.Location = new System.Drawing.Point(227, 10);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(136, 13);
            this.label19.TabIndex = 531;
            this.label19.Text = "Maximum Hearing Distance";
            // 
            // viewcone_set
            // 
            this.viewcone_set.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.viewcone_set.FormattingEnabled = true;
            this.viewcone_set.Location = new System.Drawing.Point(16, 25);
            this.viewcone_set.Name = "viewcone_set";
            this.viewcone_set.Size = new System.Drawing.Size(187, 21);
            this.viewcone_set.TabIndex = 530;
            // 
            // label17
            // 
            this.label17.AutoSize = true;
            this.label17.Location = new System.Drawing.Point(13, 10);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(73, 13);
            this.label17.TabIndex = 529;
            this.label17.Text = "Viewcone Set";
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.Controls.Add(this.tabPage5);
            this.tabControl1.Controls.Add(this.tabPage6);
            this.tabControl1.Controls.Add(this.tabPage7);
            this.tabControl1.Controls.Add(this.tabPage8);
            this.tabControl1.Location = new System.Drawing.Point(3, 99);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(435, 280);
            this.tabControl1.TabIndex = 528;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.visualSense);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(427, 254);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Visual Sense";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // visualSense
            // 
            this.visualSense.Location = new System.Drawing.Point(6, 7);
            this.visualSense.Name = "visualSense";
            this.visualSense.Size = new System.Drawing.Size(414, 241);
            this.visualSense.TabIndex = 1;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.weaponSense);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(427, 254);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Weapon Sound Sense";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // weaponSense
            // 
            this.weaponSense.Location = new System.Drawing.Point(6, 7);
            this.weaponSense.Name = "weaponSense";
            this.weaponSense.Size = new System.Drawing.Size(414, 241);
            this.weaponSense.TabIndex = 1;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.movementSense);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Size = new System.Drawing.Size(427, 254);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Movement Sound Sense";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // movementSense
            // 
            this.movementSense.Location = new System.Drawing.Point(6, 7);
            this.movementSense.Name = "movementSense";
            this.movementSense.Size = new System.Drawing.Size(414, 241);
            this.movementSense.TabIndex = 1;
            // 
            // tabPage4
            // 
            this.tabPage4.Controls.Add(this.damageSense);
            this.tabPage4.Location = new System.Drawing.Point(4, 22);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Size = new System.Drawing.Size(427, 254);
            this.tabPage4.TabIndex = 3;
            this.tabPage4.Text = "Damage Caused Sense";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // damageSense
            // 
            this.damageSense.Location = new System.Drawing.Point(6, 7);
            this.damageSense.Name = "damageSense";
            this.damageSense.Size = new System.Drawing.Size(414, 241);
            this.damageSense.TabIndex = 1;
            // 
            // tabPage5
            // 
            this.tabPage5.Controls.Add(this.touchedSense);
            this.tabPage5.Location = new System.Drawing.Point(4, 22);
            this.tabPage5.Name = "tabPage5";
            this.tabPage5.Size = new System.Drawing.Size(427, 254);
            this.tabPage5.TabIndex = 4;
            this.tabPage5.Text = "Touched Sense";
            this.tabPage5.UseVisualStyleBackColor = true;
            // 
            // touchedSense
            // 
            this.touchedSense.Location = new System.Drawing.Point(6, 7);
            this.touchedSense.Name = "touchedSense";
            this.touchedSense.Size = new System.Drawing.Size(414, 241);
            this.touchedSense.TabIndex = 1;
            // 
            // tabPage6
            // 
            this.tabPage6.Controls.Add(this.flashlightSense);
            this.tabPage6.Location = new System.Drawing.Point(4, 22);
            this.tabPage6.Name = "tabPage6";
            this.tabPage6.Size = new System.Drawing.Size(427, 254);
            this.tabPage6.TabIndex = 5;
            this.tabPage6.Text = "See Flashlight Sense";
            this.tabPage6.UseVisualStyleBackColor = true;
            // 
            // flashlightSense
            // 
            this.flashlightSense.Location = new System.Drawing.Point(6, 7);
            this.flashlightSense.Name = "flashlightSense";
            this.flashlightSense.Size = new System.Drawing.Size(414, 241);
            this.flashlightSense.TabIndex = 1;
            // 
            // tabPage7
            // 
            this.tabPage7.Controls.Add(this.flamethrowerSense);
            this.tabPage7.Location = new System.Drawing.Point(4, 22);
            this.tabPage7.Name = "tabPage7";
            this.tabPage7.Size = new System.Drawing.Size(427, 254);
            this.tabPage7.TabIndex = 6;
            this.tabPage7.Text = "Affected by Flamethrower Sense";
            this.tabPage7.UseVisualStyleBackColor = true;
            // 
            // flamethrowerSense
            // 
            this.flamethrowerSense.Location = new System.Drawing.Point(6, 7);
            this.flamethrowerSense.Name = "flamethrowerSense";
            this.flamethrowerSense.Size = new System.Drawing.Size(414, 241);
            this.flamethrowerSense.TabIndex = 1;
            // 
            // tabPage8
            // 
            this.tabPage8.Controls.Add(this.combinedSense);
            this.tabPage8.Location = new System.Drawing.Point(4, 22);
            this.tabPage8.Name = "tabPage8";
            this.tabPage8.Size = new System.Drawing.Size(427, 254);
            this.tabPage8.TabIndex = 7;
            this.tabPage8.Text = "Combined Sense";
            this.tabPage8.UseVisualStyleBackColor = true;
            // 
            // combinedSense
            // 
            this.combinedSense.Location = new System.Drawing.Point(6, 7);
            this.combinedSense.Name = "combinedSense";
            this.combinedSense.Size = new System.Drawing.Size(414, 241);
            this.combinedSense.TabIndex = 1;
            // 
            // max_hearing_distance
            // 
            this.max_hearing_distance.DecimalPlaces = 3;
            this.max_hearing_distance.Location = new System.Drawing.Point(230, 26);
            this.max_hearing_distance.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.max_hearing_distance.Name = "max_hearing_distance";
            this.max_hearing_distance.Size = new System.Drawing.Size(187, 20);
            this.max_hearing_distance.TabIndex = 577;
            // 
            // max_damage_distance_scale_to
            // 
            this.max_damage_distance_scale_to.DecimalPlaces = 3;
            this.max_damage_distance_scale_to.Location = new System.Drawing.Point(16, 65);
            this.max_damage_distance_scale_to.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.max_damage_distance_scale_to.Name = "max_damage_distance_scale_to";
            this.max_damage_distance_scale_to.Size = new System.Drawing.Size(187, 20);
            this.max_damage_distance_scale_to.TabIndex = 578;
            // 
            // SenseSet
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.max_damage_distance_scale_to);
            this.Controls.Add(this.max_hearing_distance);
            this.Controls.Add(this.label20);
            this.Controls.Add(this.label19);
            this.Controls.Add(this.viewcone_set);
            this.Controls.Add(this.label17);
            this.Controls.Add(this.tabControl1);
            this.Name = "SenseSet";
            this.Size = new System.Drawing.Size(441, 384);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
            this.tabPage4.ResumeLayout(false);
            this.tabPage5.ResumeLayout(false);
            this.tabPage6.ResumeLayout(false);
            this.tabPage7.ResumeLayout(false);
            this.tabPage8.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.max_hearing_distance)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.max_damage_distance_scale_to)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.ComboBox viewcone_set;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.TabPage tabPage4;
        private System.Windows.Forms.TabPage tabPage5;
        private System.Windows.Forms.TabPage tabPage6;
        private System.Windows.Forms.TabPage tabPage7;
        private System.Windows.Forms.TabPage tabPage8;
        private ConfigEditors.SenseType visualSense;
        private ConfigEditors.SenseType weaponSense;
        private ConfigEditors.SenseType movementSense;
        private ConfigEditors.SenseType damageSense;
        private ConfigEditors.SenseType touchedSense;
        private ConfigEditors.SenseType flashlightSense;
        private ConfigEditors.SenseType flamethrowerSense;
        private ConfigEditors.SenseType combinedSense;
        private System.Windows.Forms.NumericUpDown max_hearing_distance;
        private System.Windows.Forms.NumericUpDown max_damage_distance_scale_to;
    }
}

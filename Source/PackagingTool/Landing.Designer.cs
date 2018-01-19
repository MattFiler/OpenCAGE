namespace PackagingTool
{
    partial class Landing
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Landing));
            this.openBehaviourTreePackager = new System.Windows.Forms.Button();
            this.openCharEd = new System.Windows.Forms.Button();
            this.openAlienConfig = new System.Windows.Forms.Button();
            this.openDifficultyEditor = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.openViewconeEditor = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.openWeaponEditor = new System.Windows.Forms.Button();
            this.label6 = new System.Windows.Forms.Label();
            this.openCharViewconeEditor = new System.Windows.Forms.Button();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.label7 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.SuspendLayout();
            // 
            // openBehaviourTreePackager
            // 
            this.openBehaviourTreePackager.Location = new System.Drawing.Point(6, 116);
            this.openBehaviourTreePackager.Name = "openBehaviourTreePackager";
            this.openBehaviourTreePackager.Size = new System.Drawing.Size(229, 35);
            this.openBehaviourTreePackager.TabIndex = 1;
            this.openBehaviourTreePackager.Text = "Behaviour Tree Packager";
            this.openBehaviourTreePackager.UseVisualStyleBackColor = true;
            this.openBehaviourTreePackager.Click += new System.EventHandler(this.openBehaviourTreePackager_Click);
            // 
            // openCharEd
            // 
            this.openCharEd.Location = new System.Drawing.Point(6, 198);
            this.openCharEd.Name = "openCharEd";
            this.openCharEd.Size = new System.Drawing.Size(229, 35);
            this.openCharEd.TabIndex = 4;
            this.openCharEd.Text = "Character Attribute Editor";
            this.openCharEd.UseVisualStyleBackColor = true;
            this.openCharEd.Click += new System.EventHandler(this.button2_Click);
            // 
            // openAlienConfig
            // 
            this.openAlienConfig.Location = new System.Drawing.Point(6, 157);
            this.openAlienConfig.Name = "openAlienConfig";
            this.openAlienConfig.Size = new System.Drawing.Size(229, 35);
            this.openAlienConfig.TabIndex = 3;
            this.openAlienConfig.Text = "Alien Configuration Editor";
            this.openAlienConfig.UseVisualStyleBackColor = true;
            this.openAlienConfig.Click += new System.EventHandler(this.openAlienConfig_Click);
            // 
            // openDifficultyEditor
            // 
            this.openDifficultyEditor.Enabled = false;
            this.openDifficultyEditor.Location = new System.Drawing.Point(6, 116);
            this.openDifficultyEditor.Name = "openDifficultyEditor";
            this.openDifficultyEditor.Size = new System.Drawing.Size(229, 35);
            this.openDifficultyEditor.TabIndex = 2;
            this.openDifficultyEditor.Text = "Difficulty Setting Editor";
            this.openDifficultyEditor.UseVisualStyleBackColor = true;
            this.openDifficultyEditor.Click += new System.EventHandler(this.openDifficultyEditor_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.openDifficultyEditor);
            this.groupBox1.Controls.Add(this.openAlienConfig);
            this.groupBox1.Controls.Add(this.openCharEd);
            this.groupBox1.Location = new System.Drawing.Point(257, 9);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(240, 239);
            this.groupBox1.TabIndex = 4;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Character Configurations";
            // 
            // openViewconeEditor
            // 
            this.openViewconeEditor.Location = new System.Drawing.Point(6, 63);
            this.openViewconeEditor.Name = "openViewconeEditor";
            this.openViewconeEditor.Size = new System.Drawing.Size(229, 35);
            this.openViewconeEditor.TabIndex = 5;
            this.openViewconeEditor.Text = "Global Viewcone Editor";
            this.openViewconeEditor.UseVisualStyleBackColor = true;
            this.openViewconeEditor.Click += new System.EventHandler(this.openViewconeEditor_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 16);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(230, 91);
            this.label1.TabIndex = 4;
            this.label1.Text = resources.GetString("label1.Text");
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.openBehaviourTreePackager);
            this.groupBox2.Location = new System.Drawing.Point(11, 144);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(240, 157);
            this.groupBox2.TabIndex = 5;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Behaviour Trees";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 16);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(232, 91);
            this.label2.TabIndex = 5;
            this.label2.Text = resources.GetString("label2.Text");
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(13, 42);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(238, 24);
            this.label3.TabIndex = 6;
            this.label3.Text = "Behaviour Modding ToolKit";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(13, 70);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(238, 52);
            this.label4.TabIndex = 7;
            this.label4.Text = "This behaviour mod tool for Alien: Isolation allows\r\nyou import/export behaviour " +
    "trees and modify\r\ncharacter configurations. Currently a work in\r\nprogress!\r\n";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(44, 15);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(169, 29);
            this.label5.TabIndex = 8;
            this.label5.Text = "Alien: Isolation";
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.openWeaponEditor);
            this.groupBox3.Controls.Add(this.label6);
            this.groupBox3.Location = new System.Drawing.Point(11, 307);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(240, 92);
            this.groupBox3.TabIndex = 7;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Weapon Configurations";
            // 
            // openWeaponEditor
            // 
            this.openWeaponEditor.Location = new System.Drawing.Point(6, 51);
            this.openWeaponEditor.Name = "openWeaponEditor";
            this.openWeaponEditor.Size = new System.Drawing.Size(229, 35);
            this.openWeaponEditor.TabIndex = 6;
            this.openWeaponEditor.Text = "Weapon Editor";
            this.openWeaponEditor.UseVisualStyleBackColor = true;
            this.openWeaponEditor.Click += new System.EventHandler(this.openWeaponEditor_Click);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(3, 16);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(230, 26);
            this.label6.TabIndex = 4;
            this.label6.Text = "The editors listed below will allow you to modify \r\nweapons by ammo type.\r\n";
            // 
            // openCharViewconeEditor
            // 
            this.openCharViewconeEditor.Enabled = false;
            this.openCharViewconeEditor.Location = new System.Drawing.Point(6, 104);
            this.openCharViewconeEditor.Name = "openCharViewconeEditor";
            this.openCharViewconeEditor.Size = new System.Drawing.Size(229, 35);
            this.openCharViewconeEditor.TabIndex = 6;
            this.openCharViewconeEditor.Text = "Character Viewcone Editor";
            this.openCharViewconeEditor.UseVisualStyleBackColor = true;
            this.openCharViewconeEditor.Click += new System.EventHandler(this.openCharViewconeEditor_Click);
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.openCharViewconeEditor);
            this.groupBox4.Controls.Add(this.label7);
            this.groupBox4.Controls.Add(this.openViewconeEditor);
            this.groupBox4.Location = new System.Drawing.Point(257, 254);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(240, 145);
            this.groupBox4.TabIndex = 8;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Viewcone Configurations";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(3, 16);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(221, 39);
            this.label7.TabIndex = 4;
            this.label7.Text = "There are two types of viewcone sets, global \r\nand character specific. Character " +
    "viewcone\r\nsets draw from the global sets.";
            // 
            // Landing
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(504, 406);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Landing";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Alien: Isolation Behaviour Modding ToolKit";
            this.Load += new System.EventHandler(this.Landing_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button openBehaviourTreePackager;
        private System.Windows.Forms.Button openCharEd;
        private System.Windows.Forms.Button openAlienConfig;
        private System.Windows.Forms.Button openDifficultyEditor;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button openViewconeEditor;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Button openWeaponEditor;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button openCharViewconeEditor;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Label label7;
    }
}
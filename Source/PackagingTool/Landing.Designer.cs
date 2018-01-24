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
            this.button1 = new System.Windows.Forms.Button();
            this.openViewconeEditor = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.openWeaponEditor = new System.Windows.Forms.Button();
            this.openCharViewconeEditor = new System.Windows.Forms.Button();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.openGraphicsSettings = new System.Windows.Forms.Button();
            this.openRadiosityEditor = new System.Windows.Forms.Button();
            this.openHackEditor = new System.Windows.Forms.Button();
            this.openLoadscreenEditor = new System.Windows.Forms.Button();
            this.openBlueprintEditor = new System.Windows.Forms.Button();
            this.groupBox6 = new System.Windows.Forms.GroupBox();
            this.openLootInvSettings = new System.Windows.Forms.Button();
            this.startGame = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.groupBox6.SuspendLayout();
            this.SuspendLayout();
            // 
            // openBehaviourTreePackager
            // 
            this.openBehaviourTreePackager.Location = new System.Drawing.Point(6, 34);
            this.openBehaviourTreePackager.Name = "openBehaviourTreePackager";
            this.openBehaviourTreePackager.Size = new System.Drawing.Size(229, 35);
            this.openBehaviourTreePackager.TabIndex = 1;
            this.openBehaviourTreePackager.Text = "Behaviour Tree Packager";
            this.openBehaviourTreePackager.UseVisualStyleBackColor = true;
            this.openBehaviourTreePackager.Click += new System.EventHandler(this.openBehaviourTreePackager_Click);
            // 
            // openCharEd
            // 
            this.openCharEd.Location = new System.Drawing.Point(6, 103);
            this.openCharEd.Name = "openCharEd";
            this.openCharEd.Size = new System.Drawing.Size(229, 35);
            this.openCharEd.TabIndex = 4;
            this.openCharEd.Text = "Character Attribute Editor";
            this.openCharEd.UseVisualStyleBackColor = true;
            this.openCharEd.Click += new System.EventHandler(this.button2_Click);
            // 
            // openAlienConfig
            // 
            this.openAlienConfig.Location = new System.Drawing.Point(6, 62);
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
            this.openDifficultyEditor.Location = new System.Drawing.Point(6, 21);
            this.openDifficultyEditor.Name = "openDifficultyEditor";
            this.openDifficultyEditor.Size = new System.Drawing.Size(229, 35);
            this.openDifficultyEditor.TabIndex = 2;
            this.openDifficultyEditor.Text = "Difficulty Setting Editor";
            this.openDifficultyEditor.UseVisualStyleBackColor = true;
            this.openDifficultyEditor.Click += new System.EventHandler(this.openDifficultyEditor_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.button1);
            this.groupBox1.Controls.Add(this.openDifficultyEditor);
            this.groupBox1.Controls.Add(this.openAlienConfig);
            this.groupBox1.Controls.Add(this.openCharEd);
            this.groupBox1.Location = new System.Drawing.Point(9, 198);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(240, 187);
            this.groupBox1.TabIndex = 4;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Character Configurations";
            // 
            // button1
            // 
            this.button1.Enabled = false;
            this.button1.Location = new System.Drawing.Point(6, 144);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(229, 35);
            this.button1.TabIndex = 5;
            this.button1.Text = "Locomotion Steering Editor";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // openViewconeEditor
            // 
            this.openViewconeEditor.Location = new System.Drawing.Point(6, 20);
            this.openViewconeEditor.Name = "openViewconeEditor";
            this.openViewconeEditor.Size = new System.Drawing.Size(229, 35);
            this.openViewconeEditor.TabIndex = 5;
            this.openViewconeEditor.Text = "Viewcone Editor";
            this.openViewconeEditor.UseVisualStyleBackColor = true;
            this.openViewconeEditor.Click += new System.EventHandler(this.openViewconeEditor_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.label1);
            this.groupBox2.Controls.Add(this.openBehaviourTreePackager);
            this.groupBox2.Location = new System.Drawing.Point(9, 116);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(240, 78);
            this.groupBox2.TabIndex = 5;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Behaviour Trees";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(8, 18);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(226, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Editing behaviour trees requires LegendPlugin.";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(203, 34);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(100, 24);
            this.label3.TabIndex = 6;
            this.label3.Text = "Mod Tools";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(56, 62);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(385, 39);
            this.label4.TabIndex = 7;
            this.label4.Text = "This mod tool for Alien: Isolation allows you import/export behaviour trees, modi" +
    "fy\r\ncharacter settings and game configurations. \r\nCurrently a work in progress!\r" +
    "\n";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(170, 9);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(169, 29);
            this.label5.TabIndex = 8;
            this.label5.Text = "Alien: Isolation";
            // 
            // openWeaponEditor
            // 
            this.openWeaponEditor.Location = new System.Drawing.Point(6, 62);
            this.openWeaponEditor.Name = "openWeaponEditor";
            this.openWeaponEditor.Size = new System.Drawing.Size(229, 35);
            this.openWeaponEditor.TabIndex = 8;
            this.openWeaponEditor.Text = "Weapon Ammo Configurations";
            this.openWeaponEditor.UseVisualStyleBackColor = true;
            this.openWeaponEditor.Click += new System.EventHandler(this.openWeaponEditor_Click);
            // 
            // openCharViewconeEditor
            // 
            this.openCharViewconeEditor.Enabled = false;
            this.openCharViewconeEditor.Location = new System.Drawing.Point(6, 61);
            this.openCharViewconeEditor.Name = "openCharViewconeEditor";
            this.openCharViewconeEditor.Size = new System.Drawing.Size(229, 35);
            this.openCharViewconeEditor.TabIndex = 6;
            this.openCharViewconeEditor.Text = "Visual Sense Editor";
            this.openCharViewconeEditor.UseVisualStyleBackColor = true;
            this.openCharViewconeEditor.Click += new System.EventHandler(this.openCharViewconeEditor_Click);
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.openCharViewconeEditor);
            this.groupBox4.Controls.Add(this.openViewconeEditor);
            this.groupBox4.Location = new System.Drawing.Point(9, 389);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(240, 103);
            this.groupBox4.TabIndex = 8;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Character Vision Editors";
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.openGraphicsSettings);
            this.groupBox5.Controls.Add(this.openRadiosityEditor);
            this.groupBox5.Controls.Add(this.openHackEditor);
            this.groupBox5.Controls.Add(this.openLoadscreenEditor);
            this.groupBox5.Controls.Add(this.openBlueprintEditor);
            this.groupBox5.Location = new System.Drawing.Point(255, 226);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(240, 227);
            this.groupBox5.TabIndex = 5;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Core Game Configurations";
            // 
            // openGraphicsSettings
            // 
            this.openGraphicsSettings.Enabled = false;
            this.openGraphicsSettings.Location = new System.Drawing.Point(7, 183);
            this.openGraphicsSettings.Name = "openGraphicsSettings";
            this.openGraphicsSettings.Size = new System.Drawing.Size(229, 35);
            this.openGraphicsSettings.TabIndex = 13;
            this.openGraphicsSettings.Text = "Graphics Settings";
            this.openGraphicsSettings.UseVisualStyleBackColor = true;
            // 
            // openRadiosityEditor
            // 
            this.openRadiosityEditor.Location = new System.Drawing.Point(7, 142);
            this.openRadiosityEditor.Name = "openRadiosityEditor";
            this.openRadiosityEditor.Size = new System.Drawing.Size(229, 35);
            this.openRadiosityEditor.TabIndex = 12;
            this.openRadiosityEditor.Text = "Lighting and Character Shading Settings";
            this.openRadiosityEditor.UseVisualStyleBackColor = true;
            this.openRadiosityEditor.Click += new System.EventHandler(this.openRadiosityEditor_Click);
            // 
            // openHackEditor
            // 
            this.openHackEditor.Location = new System.Drawing.Point(7, 101);
            this.openHackEditor.Name = "openHackEditor";
            this.openHackEditor.Size = new System.Drawing.Size(229, 35);
            this.openHackEditor.TabIndex = 11;
            this.openHackEditor.Text = "Hack Tool Difficulty Editor";
            this.openHackEditor.UseVisualStyleBackColor = true;
            this.openHackEditor.Click += new System.EventHandler(this.openHackEditor_Click);
            // 
            // openLoadscreenEditor
            // 
            this.openLoadscreenEditor.Location = new System.Drawing.Point(7, 20);
            this.openLoadscreenEditor.Name = "openLoadscreenEditor";
            this.openLoadscreenEditor.Size = new System.Drawing.Size(229, 35);
            this.openLoadscreenEditor.TabIndex = 9;
            this.openLoadscreenEditor.Text = "Movie Playlist Editor";
            this.openLoadscreenEditor.UseVisualStyleBackColor = true;
            this.openLoadscreenEditor.Click += new System.EventHandler(this.button4_Click);
            // 
            // openBlueprintEditor
            // 
            this.openBlueprintEditor.Location = new System.Drawing.Point(7, 60);
            this.openBlueprintEditor.Name = "openBlueprintEditor";
            this.openBlueprintEditor.Size = new System.Drawing.Size(229, 35);
            this.openBlueprintEditor.TabIndex = 10;
            this.openBlueprintEditor.Text = "Blueprint Recipe Editor";
            this.openBlueprintEditor.UseVisualStyleBackColor = true;
            this.openBlueprintEditor.Click += new System.EventHandler(this.openBlueprintEditor_Click);
            // 
            // groupBox6
            // 
            this.groupBox6.Controls.Add(this.openWeaponEditor);
            this.groupBox6.Controls.Add(this.openLootInvSettings);
            this.groupBox6.Location = new System.Drawing.Point(255, 116);
            this.groupBox6.Name = "groupBox6";
            this.groupBox6.Size = new System.Drawing.Size(240, 104);
            this.groupBox6.TabIndex = 6;
            this.groupBox6.TabStop = false;
            this.groupBox6.Text = "Inventory Settings";
            // 
            // openLootInvSettings
            // 
            this.openLootInvSettings.Location = new System.Drawing.Point(6, 21);
            this.openLootInvSettings.Name = "openLootInvSettings";
            this.openLootInvSettings.Size = new System.Drawing.Size(229, 35);
            this.openLootInvSettings.TabIndex = 7;
            this.openLootInvSettings.Text = "Item and Inventory Settings";
            this.openLootInvSettings.UseVisualStyleBackColor = true;
            this.openLootInvSettings.Click += new System.EventHandler(this.openLootInvSettings_Click);
            // 
            // startGame
            // 
            this.startGame.Location = new System.Drawing.Point(276, 466);
            this.startGame.Name = "startGame";
            this.startGame.Size = new System.Drawing.Size(219, 26);
            this.startGame.TabIndex = 14;
            this.startGame.Text = "Launch Game";
            this.startGame.UseVisualStyleBackColor = true;
            this.startGame.Click += new System.EventHandler(this.startGame_Click);
            // 
            // Landing
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(503, 499);
            this.Controls.Add(this.startGame);
            this.Controls.Add(this.groupBox6);
            this.Controls.Add(this.groupBox5);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Landing";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Alien: Isolation Mod Tools";
            this.Load += new System.EventHandler(this.Landing_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox5.ResumeLayout(false);
            this.groupBox6.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button openBehaviourTreePackager;
        private System.Windows.Forms.Button openCharEd;
        private System.Windows.Forms.Button openAlienConfig;
        private System.Windows.Forms.Button openDifficultyEditor;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button openViewconeEditor;
        private System.Windows.Forms.Button openWeaponEditor;
        private System.Windows.Forms.Button openCharViewconeEditor;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.Button openHackEditor;
        private System.Windows.Forms.Button openLoadscreenEditor;
        private System.Windows.Forms.Button openBlueprintEditor;
        private System.Windows.Forms.GroupBox groupBox6;
        private System.Windows.Forms.Button openLootInvSettings;
        private System.Windows.Forms.Button openRadiosityEditor;
        private System.Windows.Forms.Button startGame;
        private System.Windows.Forms.Button openGraphicsSettings;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button button1;
    }
}
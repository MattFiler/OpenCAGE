namespace Alien_Isolation_Mod_Tools
{
    partial class Filemanager_ResetMod
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Filemanager_ResetMod));
            this.label1 = new System.Windows.Forms.Label();
            this.CloseButton = new System.Windows.Forms.Button();
            this.Title1 = new System.Windows.Forms.Label();
            this.ResetAll = new System.Windows.Forms.Button();
            this.HeaderText = new System.Windows.Forms.Label();
            this.HeaderImage = new System.Windows.Forms.PictureBox();
            this.Title2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.ResetGraphics = new System.Windows.Forms.Button();
            this.ResetLighting = new System.Windows.Forms.Button();
            this.ResetTrees = new System.Windows.Forms.Button();
            this.ResetAlienConfigs = new System.Windows.Forms.Button();
            this.ResetGblItem = new System.Windows.Forms.Button();
            this.ResetAmmo = new System.Windows.Forms.Button();
            this.ResetViewconesets = new System.Windows.Forms.Button();
            this.ResetDifficulties = new System.Windows.Forms.Button();
            this.ResetChrInfo = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.HeaderImage)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(699, 542);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(367, 13);
            this.label1.TabIndex = 40;
            this.label1.Text = "This will uninstall any mods and reset any changes from the mod tool creator.";
            // 
            // CloseButton
            // 
            this.CloseButton.Location = new System.Drawing.Point(1124, 799);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.Size = new System.Drawing.Size(64, 56);
            this.CloseButton.TabIndex = 39;
            this.CloseButton.Text = "CLOSE";
            this.CloseButton.UseVisualStyleBackColor = true;
            this.CloseButton.Click += new System.EventHandler(this.CloseButton_Click);
            // 
            // Title1
            // 
            this.Title1.AutoSize = true;
            this.Title1.Font = new System.Drawing.Font("Isolation", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Title1.Location = new System.Drawing.Point(696, 509);
            this.Title1.Name = "Title1";
            this.Title1.Size = new System.Drawing.Size(253, 33);
            this.Title1.TabIndex = 38;
            this.Title1.Text = "Fully Uninstall Mod";
            // 
            // ResetAll
            // 
            this.ResetAll.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ResetAll.Location = new System.Drawing.Point(702, 575);
            this.ResetAll.Name = "ResetAll";
            this.ResetAll.Size = new System.Drawing.Size(364, 73);
            this.ResetAll.TabIndex = 37;
            this.ResetAll.Text = "Reset All Files";
            this.ResetAll.UseVisualStyleBackColor = true;
            this.ResetAll.Click += new System.EventHandler(this.SelectMod_Click);
            // 
            // HeaderText
            // 
            this.HeaderText.AutoSize = true;
            this.HeaderText.BackColor = System.Drawing.Color.Transparent;
            this.HeaderText.Font = new System.Drawing.Font("Jixellation", 80.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.HeaderText.ForeColor = System.Drawing.Color.White;
            this.HeaderText.Location = new System.Drawing.Point(12, -2);
            this.HeaderText.Name = "HeaderText";
            this.HeaderText.Size = new System.Drawing.Size(361, 280);
            this.HeaderText.TabIndex = 35;
            this.HeaderText.Text = "RESET\r\nFILES";
            this.HeaderText.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // HeaderImage
            // 
            this.HeaderImage.BackgroundImage = global::Alien_Isolation_Mod_Tools.Properties.Resources.NPC_DOOR;
            this.HeaderImage.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.HeaderImage.InitialImage = global::Alien_Isolation_Mod_Tools.Properties.Resources.NPC_DOOR;
            this.HeaderImage.Location = new System.Drawing.Point(-6, -2);
            this.HeaderImage.Name = "HeaderImage";
            this.HeaderImage.Size = new System.Drawing.Size(1223, 498);
            this.HeaderImage.TabIndex = 34;
            this.HeaderImage.TabStop = false;
            // 
            // Title2
            // 
            this.Title2.AutoSize = true;
            this.Title2.Font = new System.Drawing.Font("Isolation", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Title2.Location = new System.Drawing.Point(137, 509);
            this.Title2.Name = "Title2";
            this.Title2.Size = new System.Drawing.Size(293, 33);
            this.Title2.TabIndex = 44;
            this.Title2.Text = "Partially Uninstall Mod";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(140, 542);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(507, 13);
            this.label3.TabIndex = 45;
            this.label3.Text = "Uninstall a part of a mod. This will break installed mods and allow for less data" +
    " loss for the mod tool creator.";
            // 
            // ResetGraphics
            // 
            this.ResetGraphics.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ResetGraphics.Location = new System.Drawing.Point(143, 575);
            this.ResetGraphics.Name = "ResetGraphics";
            this.ResetGraphics.Size = new System.Drawing.Size(249, 47);
            this.ResetGraphics.TabIndex = 46;
            this.ResetGraphics.Text = "Graphics Settings";
            this.ResetGraphics.UseVisualStyleBackColor = true;
            this.ResetGraphics.Click += new System.EventHandler(this.ResetGraphics_Click);
            // 
            // ResetLighting
            // 
            this.ResetLighting.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ResetLighting.Location = new System.Drawing.Point(398, 575);
            this.ResetLighting.Name = "ResetLighting";
            this.ResetLighting.Size = new System.Drawing.Size(249, 47);
            this.ResetLighting.TabIndex = 47;
            this.ResetLighting.Text = "Lighting and Character Shading";
            this.ResetLighting.UseVisualStyleBackColor = true;
            this.ResetLighting.Click += new System.EventHandler(this.ResetLighting_Click);
            // 
            // ResetTrees
            // 
            this.ResetTrees.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ResetTrees.Location = new System.Drawing.Point(398, 628);
            this.ResetTrees.Name = "ResetTrees";
            this.ResetTrees.Size = new System.Drawing.Size(249, 47);
            this.ResetTrees.TabIndex = 49;
            this.ResetTrees.Text = "Behaviour Trees";
            this.ResetTrees.UseVisualStyleBackColor = true;
            this.ResetTrees.Click += new System.EventHandler(this.ResetTrees_Click);
            // 
            // ResetAlienConfigs
            // 
            this.ResetAlienConfigs.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ResetAlienConfigs.Location = new System.Drawing.Point(143, 628);
            this.ResetAlienConfigs.Name = "ResetAlienConfigs";
            this.ResetAlienConfigs.Size = new System.Drawing.Size(249, 47);
            this.ResetAlienConfigs.TabIndex = 48;
            this.ResetAlienConfigs.Text = "Alien Configurations";
            this.ResetAlienConfigs.UseVisualStyleBackColor = true;
            this.ResetAlienConfigs.Click += new System.EventHandler(this.ResetAlienConfigs_Click);
            // 
            // ResetGblItem
            // 
            this.ResetGblItem.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ResetGblItem.Location = new System.Drawing.Point(398, 734);
            this.ResetGblItem.Name = "ResetGblItem";
            this.ResetGblItem.Size = new System.Drawing.Size(249, 100);
            this.ResetGblItem.TabIndex = 53;
            this.ResetGblItem.Text = "Movie Playlists, Blueprint Recipes, Hack Tool, Item and Inventory Settings";
            this.ResetGblItem.UseVisualStyleBackColor = true;
            this.ResetGblItem.Click += new System.EventHandler(this.ResetGblItem_Click);
            // 
            // ResetAmmo
            // 
            this.ResetAmmo.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ResetAmmo.Location = new System.Drawing.Point(143, 734);
            this.ResetAmmo.Name = "ResetAmmo";
            this.ResetAmmo.Size = new System.Drawing.Size(249, 47);
            this.ResetAmmo.TabIndex = 52;
            this.ResetAmmo.Text = "Weapon Ammo Configurations";
            this.ResetAmmo.UseVisualStyleBackColor = true;
            this.ResetAmmo.Click += new System.EventHandler(this.ResetAmmo_Click);
            // 
            // ResetViewconesets
            // 
            this.ResetViewconesets.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ResetViewconesets.Location = new System.Drawing.Point(398, 681);
            this.ResetViewconesets.Name = "ResetViewconesets";
            this.ResetViewconesets.Size = new System.Drawing.Size(249, 47);
            this.ResetViewconesets.TabIndex = 51;
            this.ResetViewconesets.Text = "Character Vision Configurations";
            this.ResetViewconesets.UseVisualStyleBackColor = true;
            this.ResetViewconesets.Click += new System.EventHandler(this.ResetViewconesets_Click);
            // 
            // ResetDifficulties
            // 
            this.ResetDifficulties.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ResetDifficulties.Location = new System.Drawing.Point(143, 681);
            this.ResetDifficulties.Name = "ResetDifficulties";
            this.ResetDifficulties.Size = new System.Drawing.Size(249, 47);
            this.ResetDifficulties.TabIndex = 50;
            this.ResetDifficulties.Text = "Difficulty Settings";
            this.ResetDifficulties.UseVisualStyleBackColor = true;
            this.ResetDifficulties.Click += new System.EventHandler(this.ResetDifficulties_Click);
            // 
            // ResetChrInfo
            // 
            this.ResetChrInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ResetChrInfo.Location = new System.Drawing.Point(143, 787);
            this.ResetChrInfo.Name = "ResetChrInfo";
            this.ResetChrInfo.Size = new System.Drawing.Size(249, 47);
            this.ResetChrInfo.TabIndex = 54;
            this.ResetChrInfo.Text = "Character Attributes, Locomotion, Sense";
            this.ResetChrInfo.UseVisualStyleBackColor = true;
            this.ResetChrInfo.Click += new System.EventHandler(this.ResetChrInfo_Click);
            // 
            // Filemanager_ResetMod
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1205, 870);
            this.Controls.Add(this.ResetChrInfo);
            this.Controls.Add(this.ResetGblItem);
            this.Controls.Add(this.ResetAmmo);
            this.Controls.Add(this.ResetViewconesets);
            this.Controls.Add(this.ResetDifficulties);
            this.Controls.Add(this.ResetTrees);
            this.Controls.Add(this.ResetAlienConfigs);
            this.Controls.Add(this.ResetLighting);
            this.Controls.Add(this.ResetGraphics);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.Title2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.CloseButton);
            this.Controls.Add(this.Title1);
            this.Controls.Add(this.ResetAll);
            this.Controls.Add(this.HeaderText);
            this.Controls.Add(this.HeaderImage);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Filemanager_ResetMod";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Alien: Isolation Mod Tools - Reset Files";
            ((System.ComponentModel.ISupportInitialize)(this.HeaderImage)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button CloseButton;
        private System.Windows.Forms.Label Title1;
        private System.Windows.Forms.Button ResetAll;
        private System.Windows.Forms.Label HeaderText;
        private System.Windows.Forms.PictureBox HeaderImage;
        private System.Windows.Forms.Label Title2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button ResetGraphics;
        private System.Windows.Forms.Button ResetLighting;
        private System.Windows.Forms.Button ResetTrees;
        private System.Windows.Forms.Button ResetAlienConfigs;
        private System.Windows.Forms.Button ResetGblItem;
        private System.Windows.Forms.Button ResetAmmo;
        private System.Windows.Forms.Button ResetViewconesets;
        private System.Windows.Forms.Button ResetDifficulties;
        private System.Windows.Forms.Button ResetChrInfo;
    }
}
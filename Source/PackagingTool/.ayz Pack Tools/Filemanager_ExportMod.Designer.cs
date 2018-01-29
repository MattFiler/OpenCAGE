namespace Alien_Isolation_Mod_Tools
{
    partial class Filemanager_ExportMod
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Filemanager_ExportMod));
            this.SaveMod = new System.Windows.Forms.Button();
            this.ModNameInput = new System.Windows.Forms.TextBox();
            this.HeaderText = new System.Windows.Forms.Label();
            this.Title1 = new System.Windows.Forms.Label();
            this.Title2 = new System.Windows.Forms.Label();
            this.Title4 = new System.Windows.Forms.Label();
            this.Title3 = new System.Windows.Forms.Label();
            this.ModDescInput = new System.Windows.Forms.TextBox();
            this.Title6 = new System.Windows.Forms.Label();
            this.Title5 = new System.Windows.Forms.Label();
            this.ModAuthorInput = new System.Windows.Forms.TextBox();
            this.HeaderImage = new System.Windows.Forms.PictureBox();
            this.CloseButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.HeaderImage)).BeginInit();
            this.SuspendLayout();
            // 
            // SaveMod
            // 
            this.SaveMod.Location = new System.Drawing.Point(258, 810);
            this.SaveMod.Name = "SaveMod";
            this.SaveMod.Size = new System.Drawing.Size(696, 36);
            this.SaveMod.TabIndex = 0;
            this.SaveMod.Text = "SAVE";
            this.SaveMod.UseVisualStyleBackColor = true;
            this.SaveMod.Click += new System.EventHandler(this.SaveMod_Click);
            // 
            // ModNameInput
            // 
            this.ModNameInput.Location = new System.Drawing.Point(258, 587);
            this.ModNameInput.Name = "ModNameInput";
            this.ModNameInput.Size = new System.Drawing.Size(696, 20);
            this.ModNameInput.TabIndex = 1;
            // 
            // HeaderText
            // 
            this.HeaderText.AutoSize = true;
            this.HeaderText.BackColor = System.Drawing.Color.Transparent;
            this.HeaderText.Font = new System.Drawing.Font("Jixellation", 80.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.HeaderText.ForeColor = System.Drawing.Color.White;
            this.HeaderText.Location = new System.Drawing.Point(796, -1);
            this.HeaderText.Name = "HeaderText";
            this.HeaderText.Size = new System.Drawing.Size(438, 280);
            this.HeaderText.TabIndex = 5;
            this.HeaderText.Text = "EXPORT\r\nMOD";
            this.HeaderText.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // Title1
            // 
            this.Title1.AutoSize = true;
            this.Title1.Font = new System.Drawing.Font("Isolation", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Title1.Location = new System.Drawing.Point(252, 529);
            this.Title1.Name = "Title1";
            this.Title1.Size = new System.Drawing.Size(149, 33);
            this.Title1.TabIndex = 6;
            this.Title1.Text = "Mod Name";
            // 
            // Title2
            // 
            this.Title2.AutoSize = true;
            this.Title2.Font = new System.Drawing.Font("Isolation", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Title2.Location = new System.Drawing.Point(255, 562);
            this.Title2.Name = "Title2";
            this.Title2.Size = new System.Drawing.Size(650, 18);
            this.Title2.TabIndex = 7;
            this.Title2.Text = "It\'s a good idea to make your mod name non-generic and unique to avoid file confl" +
    "icts.";
            // 
            // Title4
            // 
            this.Title4.AutoSize = true;
            this.Title4.Font = new System.Drawing.Font("Isolation", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Title4.Location = new System.Drawing.Point(255, 653);
            this.Title4.Name = "Title4";
            this.Title4.Size = new System.Drawing.Size(638, 18);
            this.Title4.TabIndex = 10;
            this.Title4.Text = "This description will be visible to mod users to allow them to understand its con" +
    "tents.";
            // 
            // Title3
            // 
            this.Title3.AutoSize = true;
            this.Title3.Font = new System.Drawing.Font("Isolation", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Title3.Location = new System.Drawing.Point(252, 620);
            this.Title3.Name = "Title3";
            this.Title3.Size = new System.Drawing.Size(219, 33);
            this.Title3.TabIndex = 9;
            this.Title3.Text = "Mod Description";
            // 
            // ModDescInput
            // 
            this.ModDescInput.Location = new System.Drawing.Point(258, 678);
            this.ModDescInput.Name = "ModDescInput";
            this.ModDescInput.Size = new System.Drawing.Size(696, 20);
            this.ModDescInput.TabIndex = 8;
            // 
            // Title6
            // 
            this.Title6.AutoSize = true;
            this.Title6.Font = new System.Drawing.Font("Isolation", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Title6.Location = new System.Drawing.Point(255, 744);
            this.Title6.Name = "Title6";
            this.Title6.Size = new System.Drawing.Size(343, 18);
            this.Title6.TabIndex = 13;
            this.Title6.Text = "The preferred name of the mod creator - you!";
            // 
            // Title5
            // 
            this.Title5.AutoSize = true;
            this.Title5.Font = new System.Drawing.Font("Isolation", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Title5.Location = new System.Drawing.Point(252, 711);
            this.Title5.Name = "Title5";
            this.Title5.Size = new System.Drawing.Size(163, 33);
            this.Title5.TabIndex = 12;
            this.Title5.Text = "Mod Author";
            // 
            // ModAuthorInput
            // 
            this.ModAuthorInput.Location = new System.Drawing.Point(258, 769);
            this.ModAuthorInput.Name = "ModAuthorInput";
            this.ModAuthorInput.Size = new System.Drawing.Size(696, 20);
            this.ModAuthorInput.TabIndex = 11;
            // 
            // HeaderImage
            // 
            this.HeaderImage.BackgroundImage = global::Alien_Isolation_Mod_Tools.Properties.Resources.SEEGSON_MAGAZINEMAN;
            this.HeaderImage.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.HeaderImage.InitialImage = global::Alien_Isolation_Mod_Tools.Properties.Resources.SEEGSON_MAGAZINEMAN;
            this.HeaderImage.Location = new System.Drawing.Point(-4, -1);
            this.HeaderImage.Name = "HeaderImage";
            this.HeaderImage.Size = new System.Drawing.Size(1223, 498);
            this.HeaderImage.TabIndex = 4;
            this.HeaderImage.TabStop = false;
            // 
            // CloseButton
            // 
            this.CloseButton.Location = new System.Drawing.Point(1138, 800);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.Size = new System.Drawing.Size(64, 56);
            this.CloseButton.TabIndex = 14;
            this.CloseButton.Text = "CLOSE";
            this.CloseButton.UseVisualStyleBackColor = true;
            this.CloseButton.Click += new System.EventHandler(this.CloseButton_Click);
            // 
            // Filemanager_ExportMod
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1214, 866);
            this.Controls.Add(this.CloseButton);
            this.Controls.Add(this.Title6);
            this.Controls.Add(this.Title5);
            this.Controls.Add(this.ModAuthorInput);
            this.Controls.Add(this.Title4);
            this.Controls.Add(this.Title3);
            this.Controls.Add(this.ModDescInput);
            this.Controls.Add(this.Title2);
            this.Controls.Add(this.Title1);
            this.Controls.Add(this.HeaderText);
            this.Controls.Add(this.HeaderImage);
            this.Controls.Add(this.ModNameInput);
            this.Controls.Add(this.SaveMod);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Filemanager_ExportMod";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Alien: Isolation Mod Tools - Save Mod";
            ((System.ComponentModel.ISupportInitialize)(this.HeaderImage)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button SaveMod;
        private System.Windows.Forms.TextBox ModNameInput;
        private System.Windows.Forms.PictureBox HeaderImage;
        private System.Windows.Forms.Label HeaderText;
        private System.Windows.Forms.Label Title1;
        private System.Windows.Forms.Label Title2;
        private System.Windows.Forms.Label Title4;
        private System.Windows.Forms.Label Title3;
        private System.Windows.Forms.TextBox ModDescInput;
        private System.Windows.Forms.Label Title6;
        private System.Windows.Forms.Label Title5;
        private System.Windows.Forms.TextBox ModAuthorInput;
        private System.Windows.Forms.Button CloseButton;
    }
}
namespace Alien_Isolation_Mod_Tools
{
    partial class Landing_ContentTools
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Landing_ContentTools));
            this.HeaderText = new System.Windows.Forms.Label();
            this.CloseButton = new System.Windows.Forms.Button();
            this.HeaderImage = new System.Windows.Forms.PictureBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.ModelTools = new System.Windows.Forms.Label();
            this.InterfaceTools = new System.Windows.Forms.Label();
            this.TextureTools = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.HeaderImage)).BeginInit();
            this.SuspendLayout();
            // 
            // HeaderText
            // 
            this.HeaderText.AutoSize = true;
            this.HeaderText.BackColor = System.Drawing.Color.Transparent;
            this.HeaderText.Font = new System.Drawing.Font("Jixellation", 80.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.HeaderText.ForeColor = System.Drawing.Color.White;
            this.HeaderText.Location = new System.Drawing.Point(30, 6);
            this.HeaderText.Name = "HeaderText";
            this.HeaderText.Size = new System.Drawing.Size(495, 280);
            this.HeaderText.TabIndex = 35;
            this.HeaderText.Text = "CONTENT\r\nTOOLS";
            this.HeaderText.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // CloseButton
            // 
            this.CloseButton.Location = new System.Drawing.Point(1113, 834);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.Size = new System.Drawing.Size(64, 56);
            this.CloseButton.TabIndex = 33;
            this.CloseButton.Text = "CLOSE";
            this.CloseButton.UseVisualStyleBackColor = true;
            this.CloseButton.Click += new System.EventHandler(this.CloseButton_Click);
            // 
            // HeaderImage
            // 
            this.HeaderImage.BackgroundImage = global::Alien_Isolation_Mod_Tools.Properties.Resources.TAYLOR1;
            this.HeaderImage.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.HeaderImage.InitialImage = global::Alien_Isolation_Mod_Tools.Properties.Resources.ALIEN_DOORWAY;
            this.HeaderImage.Location = new System.Drawing.Point(-24, -4);
            this.HeaderImage.Name = "HeaderImage";
            this.HeaderImage.Size = new System.Drawing.Size(1223, 494);
            this.HeaderImage.TabIndex = 34;
            this.HeaderImage.TabStop = false;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(53, 790);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(606, 24);
            this.label6.TabIndex = 64;
            this.label6.Text = "Import and export UI related files, including images and Flash GFX files.";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(52, 695);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(536, 24);
            this.label5.TabIndex = 63;
            this.label5.Text = "Import and export model files - this functionality is coming soon.";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(53, 598);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(630, 24);
            this.label4.TabIndex = 62;
            this.label4.Text = "Import and export texture files - this functionality is currently \"experimental\"." +
    "";
            // 
            // ModelTools
            // 
            this.ModelTools.AutoSize = true;
            this.ModelTools.Cursor = System.Windows.Forms.Cursors.No;
            this.ModelTools.Font = new System.Drawing.Font("Isolation", 40F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ModelTools.Location = new System.Drawing.Point(43, 628);
            this.ModelTools.Name = "ModelTools";
            this.ModelTools.Size = new System.Drawing.Size(533, 65);
            this.ModelTools.TabIndex = 61;
            this.ModelTools.Text = "Model Import/Export";
            this.ModelTools.Click += new System.EventHandler(this.ModelTools_Click);
            // 
            // InterfaceTools
            // 
            this.InterfaceTools.AutoSize = true;
            this.InterfaceTools.Cursor = System.Windows.Forms.Cursors.Hand;
            this.InterfaceTools.Font = new System.Drawing.Font("Isolation", 40F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.InterfaceTools.Location = new System.Drawing.Point(44, 724);
            this.InterfaceTools.Name = "InterfaceTools";
            this.InterfaceTools.Size = new System.Drawing.Size(434, 65);
            this.InterfaceTools.TabIndex = 60;
            this.InterfaceTools.Text = "UI Import/Export";
            this.InterfaceTools.Click += new System.EventHandler(this.InterfaceTools_Click);
            // 
            // TextureTools
            // 
            this.TextureTools.AutoSize = true;
            this.TextureTools.Cursor = System.Windows.Forms.Cursors.Hand;
            this.TextureTools.Font = new System.Drawing.Font("Isolation", 40F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TextureTools.Location = new System.Drawing.Point(43, 532);
            this.TextureTools.Name = "TextureTools";
            this.TextureTools.Size = new System.Drawing.Size(572, 65);
            this.TextureTools.TabIndex = 58;
            this.TextureTools.Text = "Texture Import/Export";
            this.TextureTools.Click += new System.EventHandler(this.TextureTools_Click);
            // 
            // Landing_ContentTools
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1189, 901);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.ModelTools);
            this.Controls.Add(this.InterfaceTools);
            this.Controls.Add(this.TextureTools);
            this.Controls.Add(this.HeaderText);
            this.Controls.Add(this.CloseButton);
            this.Controls.Add(this.HeaderImage);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "Landing_ContentTools";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "OpenCAGE - Content Tools";
            this.Load += new System.EventHandler(this.Landing_ContentTools_Load);
            ((System.ComponentModel.ISupportInitialize)(this.HeaderImage)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label HeaderText;
        private System.Windows.Forms.Button CloseButton;
        private System.Windows.Forms.PictureBox HeaderImage;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label ModelTools;
        private System.Windows.Forms.Label InterfaceTools;
        private System.Windows.Forms.Label TextureTools;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
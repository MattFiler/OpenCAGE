﻿namespace Alien_Isolation_Mod_Tools
{
    partial class Landing_ExperimentalTools
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Landing_ExperimentalTools));
            this.HeaderText = new System.Windows.Forms.Label();
            this.CloseButton = new System.Windows.Forms.Button();
            this.HeaderImage = new System.Windows.Forms.PictureBox();
            this.Title1 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.LocalisationEditor = new System.Windows.Forms.Label();
            this.KeycodeEditor = new System.Windows.Forms.Label();
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
            this.HeaderText.Size = new System.Drawing.Size(768, 280);
            this.HeaderText.TabIndex = 38;
            this.HeaderText.Text = "EXPERIMENTAL\r\nTOOLS\r\n";
            this.HeaderText.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // CloseButton
            // 
            this.CloseButton.Location = new System.Drawing.Point(1113, 834);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.Size = new System.Drawing.Size(64, 56);
            this.CloseButton.TabIndex = 36;
            this.CloseButton.Text = "CLOSE";
            this.CloseButton.UseVisualStyleBackColor = true;
            this.CloseButton.Click += new System.EventHandler(this.CloseButton_Click);
            // 
            // HeaderImage
            // 
            this.HeaderImage.BackgroundImage = global::Alien_Isolation_Mod_Tools.Properties.Resources.ANDROID_DOORWAY;
            this.HeaderImage.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.HeaderImage.InitialImage = global::Alien_Isolation_Mod_Tools.Properties.Resources.ALIEN_DOORWAY;
            this.HeaderImage.Location = new System.Drawing.Point(-24, -4);
            this.HeaderImage.Name = "HeaderImage";
            this.HeaderImage.Size = new System.Drawing.Size(1223, 494);
            this.HeaderImage.TabIndex = 37;
            this.HeaderImage.TabStop = false;
            // 
            // Title1
            // 
            this.Title1.AutoSize = true;
            this.Title1.Font = new System.Drawing.Font("Isolation", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Title1.Location = new System.Drawing.Point(12, 857);
            this.Title1.Name = "Title1";
            this.Title1.Size = new System.Drawing.Size(551, 33);
            this.Title1.TabIndex = 49;
            this.Title1.Text = "More experimental tools are coming soon!";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(52, 744);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(514, 24);
            this.label5.TabIndex = 67;
            this.label5.Text = "Edit any localised string in Alien: Isolation for each language.";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(53, 647);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(717, 24);
            this.label4.TabIndex = 66;
            this.label4.Text = "Edit any door/locker keycode in Alien: Isolation along with corresponding subtitl" +
    "es/UI.";
            // 
            // LocalisationEditor
            // 
            this.LocalisationEditor.AutoSize = true;
            this.LocalisationEditor.Cursor = System.Windows.Forms.Cursors.No;
            this.LocalisationEditor.Font = new System.Drawing.Font("Isolation", 40F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LocalisationEditor.Location = new System.Drawing.Point(43, 677);
            this.LocalisationEditor.Name = "LocalisationEditor";
            this.LocalisationEditor.Size = new System.Drawing.Size(488, 65);
            this.LocalisationEditor.TabIndex = 65;
            this.LocalisationEditor.Text = "Edit In-Game Text";
            this.LocalisationEditor.Click += new System.EventHandler(this.LocalisationEditor_Click);
            // 
            // KeycodeEditor
            // 
            this.KeycodeEditor.AutoSize = true;
            this.KeycodeEditor.Cursor = System.Windows.Forms.Cursors.Hand;
            this.KeycodeEditor.Font = new System.Drawing.Font("Isolation", 40F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.KeycodeEditor.Location = new System.Drawing.Point(43, 581);
            this.KeycodeEditor.Name = "KeycodeEditor";
            this.KeycodeEditor.Size = new System.Drawing.Size(620, 65);
            this.KeycodeEditor.TabIndex = 64;
            this.KeycodeEditor.Text = "Edit In-Game Keycodes";
            this.KeycodeEditor.Click += new System.EventHandler(this.KeycodeEditor_Click);
            // 
            // Landing_ExperimentalTools
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1189, 901);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.LocalisationEditor);
            this.Controls.Add(this.KeycodeEditor);
            this.Controls.Add(this.Title1);
            this.Controls.Add(this.HeaderText);
            this.Controls.Add(this.CloseButton);
            this.Controls.Add(this.HeaderImage);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Landing_ExperimentalTools";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Landing_ExperimentalTools";
            this.Load += new System.EventHandler(this.Landing_ExperimentalTools_Load);
            ((System.ComponentModel.ISupportInitialize)(this.HeaderImage)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label HeaderText;
        private System.Windows.Forms.Button CloseButton;
        private System.Windows.Forms.PictureBox HeaderImage;
        private System.Windows.Forms.Label Title1;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label LocalisationEditor;
        private System.Windows.Forms.Label KeycodeEditor;
    }
}
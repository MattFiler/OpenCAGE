namespace OpenCAGE.ConfigEditors
{
    partial class RadiosityEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RadiosityEditor));
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label35 = new System.Windows.Forms.Label();
            this.label34 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.gRadiosityAlbedoSaturationAmount = new System.Windows.Forms.NumericUpDown();
            this.gRadiositySpecularGlossScale = new System.Windows.Forms.NumericUpDown();
            this.gRadiosityMultiBounceScale = new System.Windows.Forms.NumericUpDown();
            this.gRadiosityEmissiveSurfaceScale = new System.Windows.Forms.NumericUpDown();
            this.gRadiosityAlbedoOverbrightAmount = new System.Windows.Forms.NumericUpDown();
            this.gRadiosityFirstBounceScale = new System.Windows.Forms.NumericUpDown();
            this.helpBtn = new System.Windows.Forms.Button();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gRadiosityAlbedoSaturationAmount)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gRadiositySpecularGlossScale)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gRadiosityMultiBounceScale)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gRadiosityEmissiveSurfaceScale)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gRadiosityAlbedoOverbrightAmount)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gRadiosityFirstBounceScale)).BeginInit();
            this.SuspendLayout();
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(15, 25);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(118, 13);
            this.label2.TabIndex = 414;
            this.label2.Text = "Emissive Surface Scale";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(15, 64);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(96, 13);
            this.label3.TabIndex = 416;
            this.label3.Text = "First Bounce Scale";
            // 
            // label35
            // 
            this.label35.AutoSize = true;
            this.label35.Location = new System.Drawing.Point(208, 25);
            this.label35.Name = "label35";
            this.label35.Size = new System.Drawing.Size(99, 13);
            this.label35.TabIndex = 418;
            this.label35.Text = "Multi Bounce Scale";
            // 
            // label34
            // 
            this.label34.AutoSize = true;
            this.label34.Location = new System.Drawing.Point(208, 64);
            this.label34.Name = "label34";
            this.label34.Size = new System.Drawing.Size(131, 13);
            this.label34.TabIndex = 420;
            this.label34.Text = "Albedo Overbright Amount";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(16, 105);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(130, 13);
            this.label4.TabIndex = 422;
            this.label4.Text = "Albedo Saturation Amount";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(208, 104);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(108, 13);
            this.label5.TabIndex = 424;
            this.label5.Text = "Specular Gloss Scale";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.gRadiosityAlbedoSaturationAmount);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.gRadiositySpecularGlossScale);
            this.groupBox2.Controls.Add(this.label34);
            this.groupBox2.Controls.Add(this.gRadiosityMultiBounceScale);
            this.groupBox2.Controls.Add(this.gRadiosityEmissiveSurfaceScale);
            this.groupBox2.Controls.Add(this.gRadiosityAlbedoOverbrightAmount);
            this.groupBox2.Controls.Add(this.label35);
            this.groupBox2.Controls.Add(this.gRadiosityFirstBounceScale);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.label4);
            this.groupBox2.Controls.Add(this.label5);
            this.groupBox2.Location = new System.Drawing.Point(12, 12);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(417, 155);
            this.groupBox2.TabIndex = 431;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Radiosity Lighting Settings";
            // 
            // gRadiosityAlbedoSaturationAmount
            // 
            this.gRadiosityAlbedoSaturationAmount.DecimalPlaces = 6;
            this.gRadiosityAlbedoSaturationAmount.Location = new System.Drawing.Point(19, 121);
            this.gRadiosityAlbedoSaturationAmount.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.gRadiosityAlbedoSaturationAmount.Name = "gRadiosityAlbedoSaturationAmount";
            this.gRadiosityAlbedoSaturationAmount.Size = new System.Drawing.Size(187, 20);
            this.gRadiosityAlbedoSaturationAmount.TabIndex = 440;
            // 
            // gRadiositySpecularGlossScale
            // 
            this.gRadiositySpecularGlossScale.DecimalPlaces = 6;
            this.gRadiositySpecularGlossScale.Location = new System.Drawing.Point(211, 121);
            this.gRadiositySpecularGlossScale.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.gRadiositySpecularGlossScale.Name = "gRadiositySpecularGlossScale";
            this.gRadiositySpecularGlossScale.Size = new System.Drawing.Size(187, 20);
            this.gRadiositySpecularGlossScale.TabIndex = 439;
            // 
            // gRadiosityMultiBounceScale
            // 
            this.gRadiosityMultiBounceScale.DecimalPlaces = 6;
            this.gRadiosityMultiBounceScale.Location = new System.Drawing.Point(211, 41);
            this.gRadiosityMultiBounceScale.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.gRadiosityMultiBounceScale.Name = "gRadiosityMultiBounceScale";
            this.gRadiosityMultiBounceScale.Size = new System.Drawing.Size(187, 20);
            this.gRadiosityMultiBounceScale.TabIndex = 438;
            // 
            // gRadiosityEmissiveSurfaceScale
            // 
            this.gRadiosityEmissiveSurfaceScale.DecimalPlaces = 6;
            this.gRadiosityEmissiveSurfaceScale.Location = new System.Drawing.Point(19, 41);
            this.gRadiosityEmissiveSurfaceScale.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.gRadiosityEmissiveSurfaceScale.Name = "gRadiosityEmissiveSurfaceScale";
            this.gRadiosityEmissiveSurfaceScale.Size = new System.Drawing.Size(187, 20);
            this.gRadiosityEmissiveSurfaceScale.TabIndex = 436;
            // 
            // gRadiosityAlbedoOverbrightAmount
            // 
            this.gRadiosityAlbedoOverbrightAmount.DecimalPlaces = 6;
            this.gRadiosityAlbedoOverbrightAmount.Location = new System.Drawing.Point(211, 81);
            this.gRadiosityAlbedoOverbrightAmount.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.gRadiosityAlbedoOverbrightAmount.Name = "gRadiosityAlbedoOverbrightAmount";
            this.gRadiosityAlbedoOverbrightAmount.Size = new System.Drawing.Size(187, 20);
            this.gRadiosityAlbedoOverbrightAmount.TabIndex = 437;
            // 
            // gRadiosityFirstBounceScale
            // 
            this.gRadiosityFirstBounceScale.DecimalPlaces = 6;
            this.gRadiosityFirstBounceScale.Location = new System.Drawing.Point(19, 81);
            this.gRadiosityFirstBounceScale.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.gRadiosityFirstBounceScale.Name = "gRadiosityFirstBounceScale";
            this.gRadiosityFirstBounceScale.Size = new System.Drawing.Size(187, 20);
            this.gRadiosityFirstBounceScale.TabIndex = 435;
            // 
            // helpBtn
            // 
            this.helpBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.helpBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.helpBtn.Image = ((System.Drawing.Image)(resources.GetObject("helpBtn.Image")));
            this.helpBtn.Location = new System.Drawing.Point(420, 0);
            this.helpBtn.Name = "helpBtn";
            this.helpBtn.Size = new System.Drawing.Size(20, 20);
            this.helpBtn.TabIndex = 476;
            this.helpBtn.UseVisualStyleBackColor = true;
            this.helpBtn.Click += new System.EventHandler(this.helpBtn_Click);
            // 
            // RadiosityEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(440, 177);
            this.Controls.Add(this.helpBtn);
            this.Controls.Add(this.groupBox2);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "RadiosityEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Radiosity Editor";
            this.Load += new System.EventHandler(this.RadiosityEditor_Load);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gRadiosityAlbedoSaturationAmount)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gRadiositySpecularGlossScale)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gRadiosityMultiBounceScale)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gRadiosityEmissiveSurfaceScale)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gRadiosityAlbedoOverbrightAmount)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gRadiosityFirstBounceScale)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label35;
        private System.Windows.Forms.Label label34;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.NumericUpDown gRadiosityFirstBounceScale;
        private System.Windows.Forms.NumericUpDown gRadiosityEmissiveSurfaceScale;
        private System.Windows.Forms.NumericUpDown gRadiosityAlbedoOverbrightAmount;
        private System.Windows.Forms.NumericUpDown gRadiosityMultiBounceScale;
        private System.Windows.Forms.NumericUpDown gRadiosityAlbedoSaturationAmount;
        private System.Windows.Forms.NumericUpDown gRadiositySpecularGlossScale;
        private System.Windows.Forms.Button helpBtn;
    }
}

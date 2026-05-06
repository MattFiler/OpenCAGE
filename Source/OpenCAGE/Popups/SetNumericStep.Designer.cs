namespace CommandsEditor.Popups
{
    partial class SetNumericStep
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SetNumericStep));
            this.posStep = new System.Windows.Forms.NumericUpDown();
            this.rotStep = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.posStep)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.rotStep)).BeginInit();
            this.SuspendLayout();
            // 
            // posStep
            // 
            this.posStep.DecimalPlaces = 6;
            this.posStep.Location = new System.Drawing.Point(143, 12);
            this.posStep.Name = "posStep";
            this.posStep.Size = new System.Drawing.Size(120, 20);
            this.posStep.TabIndex = 0;
            this.posStep.ValueChanged += new System.EventHandler(this.posStep_ValueChanged);
            // 
            // rotStep
            // 
            this.rotStep.DecimalPlaces = 6;
            this.rotStep.Location = new System.Drawing.Point(143, 38);
            this.rotStep.Name = "rotStep";
            this.rotStep.Size = new System.Drawing.Size(120, 20);
            this.rotStep.TabIndex = 1;
            this.rotStep.ValueChanged += new System.EventHandler(this.rotStep_ValueChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(14, 14);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(123, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Numeric Step (Position): ";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(11, 40);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(126, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Numeric Step (Rotation): ";
            // 
            // SetNumericStep
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(277, 70);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.rotStep);
            this.Controls.Add(this.posStep);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.Name = "SetNumericStep";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Set Numeric Step";
            ((System.ComponentModel.ISupportInitialize)(this.posStep)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.rotStep)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.NumericUpDown posStep;
        private System.Windows.Forms.NumericUpDown rotStep;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
    }
}

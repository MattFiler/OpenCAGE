namespace OpenCAGE
{
    partial class CAGEAnimation_SelectParameter
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CAGEAnimation_SelectParameter));
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.select_param = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.parameters = new System.Windows.Forms.ComboBox();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.select_param);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.parameters);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(776, 92);
            this.groupBox1.TabIndex = 5;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Select Entity Parameter To Animate";
            // 
            // select_param
            // 
            this.select_param.Location = new System.Drawing.Point(644, 51);
            this.select_param.Name = "select_param";
            this.select_param.Size = new System.Drawing.Size(101, 23);
            this.select_param.TabIndex = 4;
            this.select_param.Text = "Select";
            this.select_param.UseVisualStyleBackColor = true;
            this.select_param.Click += new System.EventHandler(this.select_param_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(41, 27);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(55, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Parameter";
            // 
            // parameters
            // 
            this.parameters.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.parameters.FormattingEnabled = true;
            this.parameters.Items.AddRange(new object[] {
            "POSITION",
            "FLOAT",
            "STRING",
            "SPLINE_DATA",
            "ENUM",
            "SHORT_GUID",
            "FILEPATH",
            "BOOL",
            "DIRECTION",
            "INTEGER"});
            this.parameters.Location = new System.Drawing.Point(104, 24);
            this.parameters.Name = "parameters";
            this.parameters.Size = new System.Drawing.Size(641, 21);
            this.parameters.TabIndex = 0;
            // 
            // CAGEAnimation_SelectParameter
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 117);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.Name = "CAGEAnimation_SelectParameter";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Select Entity Parameter To Animate";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button select_param;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox parameters;
    }
}

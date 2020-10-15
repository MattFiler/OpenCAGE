namespace Alien_Isolation_Mod_Tools
{
    partial class CSE_Alpha_EditPin
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CSE_Alpha_EditPin));
            this.pin_link_id = new System.Windows.Forms.Label();
            this.pin_out_node = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.pin_out_param = new System.Windows.Forms.ComboBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.pin_in_param = new System.Windows.Forms.ComboBox();
            this.pin_in_node = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.save_pin = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // pin_link_id
            // 
            this.pin_link_id.AutoSize = true;
            this.pin_link_id.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.pin_link_id.Location = new System.Drawing.Point(132, 13);
            this.pin_link_id.Name = "pin_link_id";
            this.pin_link_id.Size = new System.Drawing.Size(163, 20);
            this.pin_link_id.TabIndex = 0;
            this.pin_link_id.Text = "Pin Link [00-00-00-00]";
            // 
            // pin_out_node
            // 
            this.pin_out_node.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.pin_out_node.FormattingEnabled = true;
            this.pin_out_node.Location = new System.Drawing.Point(16, 40);
            this.pin_out_node.Name = "pin_out_node";
            this.pin_out_node.Size = new System.Drawing.Size(367, 21);
            this.pin_out_node.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 24);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(120, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Connects out from node";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(13, 64);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(84, 13);
            this.label3.TabIndex = 3;
            this.label3.Text = "Using parameter";
            // 
            // pin_out_param
            // 
            this.pin_out_param.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.pin_out_param.FormattingEnabled = true;
            this.pin_out_param.Location = new System.Drawing.Point(16, 80);
            this.pin_out_param.Name = "pin_out_param";
            this.pin_out_param.Size = new System.Drawing.Size(367, 21);
            this.pin_out_param.TabIndex = 4;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.pin_out_param);
            this.groupBox1.Controls.Add(this.pin_out_node);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Location = new System.Drawing.Point(16, 42);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(397, 116);
            this.groupBox1.TabIndex = 9;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Pin Out";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.pin_in_param);
            this.groupBox2.Controls.Add(this.pin_in_node);
            this.groupBox2.Controls.Add(this.label6);
            this.groupBox2.Controls.Add(this.label7);
            this.groupBox2.Location = new System.Drawing.Point(16, 164);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(397, 116);
            this.groupBox2.TabIndex = 10;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Pin In";
            // 
            // pin_in_param
            // 
            this.pin_in_param.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.pin_in_param.FormattingEnabled = true;
            this.pin_in_param.Location = new System.Drawing.Point(16, 80);
            this.pin_in_param.Name = "pin_in_param";
            this.pin_in_param.Size = new System.Drawing.Size(367, 21);
            this.pin_in_param.TabIndex = 4;
            // 
            // pin_in_node
            // 
            this.pin_in_node.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.pin_in_node.FormattingEnabled = true;
            this.pin_in_node.Location = new System.Drawing.Point(16, 40);
            this.pin_in_node.Name = "pin_in_node";
            this.pin_in_node.Size = new System.Drawing.Size(367, 21);
            this.pin_in_node.TabIndex = 1;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(13, 24);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(102, 13);
            this.label6.TabIndex = 2;
            this.label6.Text = "Connects in to node";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(13, 64);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(84, 13);
            this.label7.TabIndex = 3;
            this.label7.Text = "Using parameter";
            // 
            // save_pin
            // 
            this.save_pin.Location = new System.Drawing.Point(308, 298);
            this.save_pin.Name = "save_pin";
            this.save_pin.Size = new System.Drawing.Size(105, 23);
            this.save_pin.TabIndex = 11;
            this.save_pin.Text = "Save";
            this.save_pin.UseVisualStyleBackColor = true;
            this.save_pin.Click += new System.EventHandler(this.save_pin_Click);
            // 
            // CSE_Alpha_EditPin
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(438, 339);
            this.Controls.Add(this.save_pin);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.pin_link_id);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "CSE_Alpha_EditPin";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Edit Node Pin Link";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label pin_link_id;
        private System.Windows.Forms.ComboBox pin_out_node;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox pin_out_param;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.ComboBox pin_in_param;
        private System.Windows.Forms.ComboBox pin_in_node;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button save_pin;
    }
}
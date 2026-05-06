namespace OpenCAGE
{
    partial class AddOrEditLink
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddOrEditLink));
            this.label3 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.selectEntityOut = new System.Windows.Forms.Button();
            this.parentParameterList = new System.Windows.Forms.ComboBox();
            this.parentEntityList = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.selectEntityIn = new System.Windows.Forms.Button();
            this.childParameterList = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.childEntityList = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.save_pin = new System.Windows.Forms.Button();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(13, 64);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(117, 13);
            this.label3.TabIndex = 3;
            this.label3.Text = "Parameter on this entity";
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.selectEntityOut);
            this.groupBox1.Controls.Add(this.parentParameterList);
            this.groupBox1.Controls.Add(this.parentEntityList);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(876, 116);
            this.groupBox1.TabIndex = 9;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Pin Out";
            // 
            // selectEntityOut
            // 
            this.selectEntityOut.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.selectEntityOut.Location = new System.Drawing.Point(718, 39);
            this.selectEntityOut.Name = "selectEntityOut";
            this.selectEntityOut.Size = new System.Drawing.Size(142, 23);
            this.selectEntityOut.TabIndex = 13;
            this.selectEntityOut.Text = "Browse Entity";
            this.selectEntityOut.UseVisualStyleBackColor = true;
            this.selectEntityOut.Click += new System.EventHandler(this.selectEntityOut_Click);
            // 
            // parentParameterList
            // 
            this.parentParameterList.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.parentParameterList.FormattingEnabled = true;
            this.parentParameterList.Location = new System.Drawing.Point(16, 79);
            this.parentParameterList.Name = "parentParameterList";
            this.parentParameterList.Size = new System.Drawing.Size(844, 21);
            this.parentParameterList.TabIndex = 8;
            // 
            // parentEntityList
            // 
            this.parentEntityList.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.parentEntityList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.parentEntityList.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.parentEntityList.FormattingEnabled = true;
            this.parentEntityList.Location = new System.Drawing.Point(16, 40);
            this.parentEntityList.Name = "parentEntityList";
            this.parentEntityList.Size = new System.Drawing.Size(696, 21);
            this.parentEntityList.TabIndex = 6;
            this.parentEntityList.SelectedIndexChanged += new System.EventHandler(this.pin_out_node_SelectedIndexChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 24);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(121, 13);
            this.label2.TabIndex = 7;
            this.label2.Text = "Connects out from entity";
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Controls.Add(this.selectEntityIn);
            this.groupBox2.Controls.Add(this.childParameterList);
            this.groupBox2.Controls.Add(this.label1);
            this.groupBox2.Controls.Add(this.childEntityList);
            this.groupBox2.Controls.Add(this.label6);
            this.groupBox2.Location = new System.Drawing.Point(12, 222);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(876, 116);
            this.groupBox2.TabIndex = 10;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Pin In";
            // 
            // selectEntityIn
            // 
            this.selectEntityIn.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.selectEntityIn.Location = new System.Drawing.Point(718, 39);
            this.selectEntityIn.Name = "selectEntityIn";
            this.selectEntityIn.Size = new System.Drawing.Size(142, 23);
            this.selectEntityIn.TabIndex = 14;
            this.selectEntityIn.Text = "Browse Entity";
            this.selectEntityIn.UseVisualStyleBackColor = true;
            this.selectEntityIn.Click += new System.EventHandler(this.selectEntityIn_Click);
            // 
            // childParameterList
            // 
            this.childParameterList.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.childParameterList.FormattingEnabled = true;
            this.childParameterList.Location = new System.Drawing.Point(16, 80);
            this.childParameterList.Name = "childParameterList";
            this.childParameterList.Size = new System.Drawing.Size(844, 21);
            this.childParameterList.TabIndex = 9;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 64);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(117, 13);
            this.label1.TabIndex = 6;
            this.label1.Text = "Parameter on this entity";
            // 
            // childEntityList
            // 
            this.childEntityList.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.childEntityList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.childEntityList.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.childEntityList.FormattingEnabled = true;
            this.childEntityList.Location = new System.Drawing.Point(16, 40);
            this.childEntityList.Name = "childEntityList";
            this.childEntityList.Size = new System.Drawing.Size(696, 21);
            this.childEntityList.TabIndex = 1;
            this.childEntityList.SelectedIndexChanged += new System.EventHandler(this.pin_in_node_SelectedIndexChanged);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(13, 24);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(103, 13);
            this.label6.TabIndex = 2;
            this.label6.Text = "Connects in to entity";
            // 
            // save_pin
            // 
            this.save_pin.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.save_pin.Location = new System.Drawing.Point(783, 344);
            this.save_pin.Name = "save_pin";
            this.save_pin.Size = new System.Drawing.Size(105, 23);
            this.save_pin.TabIndex = 11;
            this.save_pin.Text = "Save";
            this.save_pin.UseVisualStyleBackColor = true;
            this.save_pin.Click += new System.EventHandler(this.save_pin_Click);
            // 
            // pictureBox1
            // 
            this.pictureBox1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.pictureBox1.BackgroundImage = global::OpenCAGE.Properties.Resources.arrow;
            this.pictureBox1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.pictureBox1.InitialImage = global::OpenCAGE.Properties.Resources.arrow;
            this.pictureBox1.Location = new System.Drawing.Point(431, 134);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(34, 82);
            this.pictureBox1.TabIndex = 12;
            this.pictureBox1.TabStop = false;
            // 
            // AddOrEditLink
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 377);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.save_pin);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(1500, 416);
            this.MinimumSize = new System.Drawing.Size(500, 416);
            this.Name = "AddOrEditLink";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Edit Entity Link";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.ComboBox childEntityList;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button save_pin;
        private System.Windows.Forms.ComboBox parentEntityList;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.ComboBox parentParameterList;
        private System.Windows.Forms.ComboBox childParameterList;
        private System.Windows.Forms.Button selectEntityOut;
        private System.Windows.Forms.Button selectEntityIn;
    }
}

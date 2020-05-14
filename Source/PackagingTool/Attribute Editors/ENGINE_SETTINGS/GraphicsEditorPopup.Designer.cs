namespace Alien_Isolation_Mod_Tools.Attribute_Editors.ENGINE_SETTINGS
{
    partial class GraphicsEditorPopup
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GraphicsEditorPopup));
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.TitleTwo = new System.Windows.Forms.Label();
            this.textBox3 = new System.Windows.Forms.TextBox();
            this.TitleThree = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.TitleOne = new System.Windows.Forms.Label();
            this.label78 = new System.Windows.Forms.Label();
            this.btnSave = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.textBox2);
            this.groupBox1.Controls.Add(this.TitleTwo);
            this.groupBox1.Controls.Add(this.textBox3);
            this.groupBox1.Controls.Add(this.TitleThree);
            this.groupBox1.Controls.Add(this.textBox1);
            this.groupBox1.Controls.Add(this.TitleOne);
            this.groupBox1.Location = new System.Drawing.Point(7, 41);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(439, 120);
            this.groupBox1.TabIndex = 332;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "New Item Info";
            // 
            // textBox2
            // 
            this.textBox2.Location = new System.Drawing.Point(14, 85);
            this.textBox2.Name = "textBox2";
            this.textBox2.Size = new System.Drawing.Size(187, 20);
            this.textBox2.TabIndex = 250;
            // 
            // TitleTwo
            // 
            this.TitleTwo.AutoSize = true;
            this.TitleTwo.Location = new System.Drawing.Point(11, 69);
            this.TitleTwo.Name = "TitleTwo";
            this.TitleTwo.Size = new System.Drawing.Size(128, 13);
            this.TitleTwo.TabIndex = 249;
            this.TitleTwo.Text = "PLACEHOLDER TITLE 2";
            // 
            // textBox3
            // 
            this.textBox3.Location = new System.Drawing.Point(230, 85);
            this.textBox3.Name = "textBox3";
            this.textBox3.Size = new System.Drawing.Size(187, 20);
            this.textBox3.TabIndex = 248;
            // 
            // TitleThree
            // 
            this.TitleThree.AutoSize = true;
            this.TitleThree.Location = new System.Drawing.Point(227, 69);
            this.TitleThree.Name = "TitleThree";
            this.TitleThree.Size = new System.Drawing.Size(128, 13);
            this.TitleThree.TabIndex = 247;
            this.TitleThree.Text = "PLACEHOLDER TITLE 3";
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(14, 42);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(403, 20);
            this.textBox1.TabIndex = 246;
            // 
            // TitleOne
            // 
            this.TitleOne.AutoSize = true;
            this.TitleOne.Location = new System.Drawing.Point(11, 26);
            this.TitleOne.Name = "TitleOne";
            this.TitleOne.Size = new System.Drawing.Size(128, 13);
            this.TitleOne.TabIndex = 245;
            this.TitleOne.Text = "PLACEHOLDER TITLE 1";
            // 
            // label78
            // 
            this.label78.AutoSize = true;
            this.label78.Font = new System.Drawing.Font("Microsoft Sans Serif", 17F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label78.Location = new System.Drawing.Point(11, 9);
            this.label78.Name = "label78";
            this.label78.Size = new System.Drawing.Size(434, 29);
            this.label78.TabIndex = 331;
            this.label78.Text = "Alien: Isolation Graphics Settings Editor";
            this.label78.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(265, 167);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(181, 30);
            this.btnSave.TabIndex = 330;
            this.btnSave.Text = "Add Item";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // GraphicsEditorPopup
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(452, 203);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.label78);
            this.Controls.Add(this.btnSave);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "GraphicsEditorPopup";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Alien: Isolation Graphics Setting Editor";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label78;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label TitleOne;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.Label TitleTwo;
        private System.Windows.Forms.TextBox textBox3;
        private System.Windows.Forms.Label TitleThree;
    }
}
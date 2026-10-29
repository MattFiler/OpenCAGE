namespace OpenCAGE.Popups.UserControls
{
    partial class GUI_Resource_DynamicPhysicsSystem
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.labelPhysics = new System.Windows.Forms.Label();
            this.physicsName = new System.Windows.Forms.TextBox();
            this.btnSet = new System.Windows.Forms.Button();
            this.btnClear = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.labelPhysics);
            this.groupBox1.Controls.Add(this.physicsName);
            this.groupBox1.Controls.Add(this.btnSet);
            this.groupBox1.Controls.Add(this.btnClear);
            this.groupBox1.Location = new System.Drawing.Point(3, 3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(832, 58);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Dynamic Physics System";
            // 
            // labelPhysics
            // 
            this.labelPhysics.AutoSize = true;
            this.labelPhysics.Location = new System.Drawing.Point(18, 28);
            this.labelPhysics.Name = "labelPhysics";
            this.labelPhysics.Size = new System.Drawing.Size(82, 13);
            this.labelPhysics.TabIndex = 0;
            this.labelPhysics.Text = "Physics system:";
            // 
            // physicsName
            // 
            this.physicsName.Location = new System.Drawing.Point(106, 25);
            this.physicsName.Name = "physicsName";
            this.physicsName.ReadOnly = true;
            this.physicsName.Size = new System.Drawing.Size(483, 20);
            this.physicsName.TabIndex = 1;
            // 
            // btnSet
            // 
            this.btnSet.Location = new System.Drawing.Point(595, 23);
            this.btnSet.Name = "btnSet";
            this.btnSet.Size = new System.Drawing.Size(110, 23);
            this.btnSet.TabIndex = 2;
            this.btnSet.Text = "Set Physics...";
            this.btnSet.UseVisualStyleBackColor = true;
            this.btnSet.Click += new System.EventHandler(this.btnSet_Click);
            // 
            // btnClear
            // 
            this.btnClear.Location = new System.Drawing.Point(711, 23);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new System.Drawing.Size(75, 23);
            this.btnClear.TabIndex = 3;
            this.btnClear.Text = "Clear";
            this.btnClear.UseVisualStyleBackColor = true;
            this.btnClear.Click += new System.EventHandler(this.btnClear_Click);
            // 
            // GUI_Resource_DynamicPhysicsSystem
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBox1);
            this.Name = "GUI_Resource_DynamicPhysicsSystem";
            this.Size = new System.Drawing.Size(838, 64);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label labelPhysics;
        private System.Windows.Forms.TextBox physicsName;
        private System.Windows.Forms.Button btnSet;
        private System.Windows.Forms.Button btnClear;
    }
}

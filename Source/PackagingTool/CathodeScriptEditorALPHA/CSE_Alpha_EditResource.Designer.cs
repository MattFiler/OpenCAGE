namespace Alien_Isolation_Mod_Tools
{
    partial class CSE_Alpha_EditResource
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CSE_Alpha_EditResource));
            this.submesh_list = new System.Windows.Forms.GroupBox();
            this.submesh_count = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            this.save_changes = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.submesh_count)).BeginInit();
            this.SuspendLayout();
            // 
            // submesh_list
            // 
            this.submesh_list.Location = new System.Drawing.Point(12, 38);
            this.submesh_list.Name = "submesh_list";
            this.submesh_list.Size = new System.Drawing.Size(1483, 71);
            this.submesh_list.TabIndex = 0;
            this.submesh_list.TabStop = false;
            this.submesh_list.Text = "Referenced Models";
            // 
            // submesh_count
            // 
            this.submesh_count.Location = new System.Drawing.Point(1375, 12);
            this.submesh_count.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.submesh_count.Name = "submesh_count";
            this.submesh_count.Size = new System.Drawing.Size(120, 20);
            this.submesh_count.TabIndex = 2;
            this.submesh_count.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(1302, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(70, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "Model Count:";
            // 
            // save_changes
            // 
            this.save_changes.Location = new System.Drawing.Point(12, 9);
            this.save_changes.Name = "save_changes";
            this.save_changes.Size = new System.Drawing.Size(126, 23);
            this.save_changes.TabIndex = 4;
            this.save_changes.Text = "Save";
            this.save_changes.UseVisualStyleBackColor = true;
            this.save_changes.Click += new System.EventHandler(this.save_changes_Click);
            // 
            // CSE_Alpha_EditResource
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1507, 120);
            this.Controls.Add(this.save_changes);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.submesh_count);
            this.Controls.Add(this.submesh_list);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "CSE_Alpha_EditResource";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Edit Resource Content";
            ((System.ComponentModel.ISupportInitialize)(this.submesh_count)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox submesh_list;
        private System.Windows.Forms.NumericUpDown submesh_count;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button save_changes;
    }
}
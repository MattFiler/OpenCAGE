namespace Alien_Isolation_Mod_Tools.Attribute_Editors.Misc
{
    partial class KeycodeEditor
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
            this.label78 = new System.Windows.Forms.Label();
            this.treeView1 = new System.Windows.Forms.TreeView();
            this.SuspendLayout();
            // 
            // label78
            // 
            this.label78.AutoSize = true;
            this.label78.Font = new System.Drawing.Font("Microsoft Sans Serif", 17F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label78.Location = new System.Drawing.Point(302, 9);
            this.label78.Name = "label78";
            this.label78.Size = new System.Drawing.Size(340, 29);
            this.label78.TabIndex = 413;
            this.label78.Text = "Alien: Isolation Keycode Editor";
            // 
            // treeView1
            // 
            this.treeView1.Location = new System.Drawing.Point(12, 70);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(367, 532);
            this.treeView1.TabIndex = 414;
            // 
            // KeycodeEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(996, 614);
            this.Controls.Add(this.treeView1);
            this.Controls.Add(this.label78);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "KeycodeEditor";
            this.Text = "Alien: Isolation Keycode Editor";
            this.Load += new System.EventHandler(this.KeycodeEditor_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label78;
        private System.Windows.Forms.TreeView treeView1;
    }
}
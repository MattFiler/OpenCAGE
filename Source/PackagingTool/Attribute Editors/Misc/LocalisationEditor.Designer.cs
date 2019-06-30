namespace Alien_Isolation_Mod_Tools.Attribute_Editors.Misc
{
    partial class LocalisationEditor
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
            this.loadDemo = new System.Windows.Forms.Button();
            this.stringID = new System.Windows.Forms.TextBox();
            this.stringOut = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // loadDemo
            // 
            this.loadDemo.Location = new System.Drawing.Point(370, 42);
            this.loadDemo.Name = "loadDemo";
            this.loadDemo.Size = new System.Drawing.Size(75, 23);
            this.loadDemo.TabIndex = 0;
            this.loadDemo.Text = "Load!";
            this.loadDemo.UseVisualStyleBackColor = true;
            this.loadDemo.Click += new System.EventHandler(this.loadDemo_Click);
            // 
            // stringID
            // 
            this.stringID.Location = new System.Drawing.Point(71, 44);
            this.stringID.Name = "stringID";
            this.stringID.Size = new System.Drawing.Size(293, 20);
            this.stringID.TabIndex = 1;
            // 
            // stringOut
            // 
            this.stringOut.Location = new System.Drawing.Point(71, 70);
            this.stringOut.Multiline = true;
            this.stringOut.Name = "stringOut";
            this.stringOut.Size = new System.Drawing.Size(374, 132);
            this.stringOut.TabIndex = 2;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(68, 28);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(112, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "Load a localised string";
            // 
            // LocalisationEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.stringOut);
            this.Controls.Add(this.stringID);
            this.Controls.Add(this.loadDemo);
            this.Name = "LocalisationEditor";
            this.Text = "LocalisationEditor";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button loadDemo;
        private System.Windows.Forms.TextBox stringID;
        private System.Windows.Forms.TextBox stringOut;
        private System.Windows.Forms.Label label1;
    }
}
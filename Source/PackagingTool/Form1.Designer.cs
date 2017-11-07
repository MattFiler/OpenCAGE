namespace PackagingTool
{
    partial class Form1
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
            this.unpackButton = new System.Windows.Forms.Button();
            this.repackButton = new System.Windows.Forms.Button();
            this.resetTrees = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // unpackButton
            // 
            this.unpackButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.unpackButton.Location = new System.Drawing.Point(12, 12);
            this.unpackButton.Name = "unpackButton";
            this.unpackButton.Size = new System.Drawing.Size(280, 51);
            this.unpackButton.TabIndex = 2;
            this.unpackButton.Text = "Export Behaviour Trees";
            this.unpackButton.UseVisualStyleBackColor = true;
            this.unpackButton.Click += new System.EventHandler(this.unpackButton_Click);
            // 
            // repackButton
            // 
            this.repackButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.repackButton.Location = new System.Drawing.Point(12, 69);
            this.repackButton.Name = "repackButton";
            this.repackButton.Size = new System.Drawing.Size(280, 51);
            this.repackButton.TabIndex = 3;
            this.repackButton.Text = "Import Behaviour Trees";
            this.repackButton.UseVisualStyleBackColor = true;
            this.repackButton.Click += new System.EventHandler(this.repackButton_Click);
            // 
            // resetTrees
            // 
            this.resetTrees.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.resetTrees.Location = new System.Drawing.Point(12, 126);
            this.resetTrees.Name = "resetTrees";
            this.resetTrees.Size = new System.Drawing.Size(280, 51);
            this.resetTrees.TabIndex = 4;
            this.resetTrees.Text = "Reset Behaviour Trees";
            this.resetTrees.UseVisualStyleBackColor = true;
            this.resetTrees.Click += new System.EventHandler(this.resetTrees_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(306, 187);
            this.Controls.Add(this.resetTrees);
            this.Controls.Add(this.repackButton);
            this.Controls.Add(this.unpackButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "Form1";
            this.Text = "Alien: Isolation Behaviour Packager";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button unpackButton;
        private System.Windows.Forms.Button repackButton;
        private System.Windows.Forms.Button resetTrees;
    }
}


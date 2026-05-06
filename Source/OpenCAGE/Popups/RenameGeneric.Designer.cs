namespace CommandsEditor
{
    partial class RenameGeneric
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RenameGeneric));
            this.entity_name = new System.Windows.Forms.TextBox();
            this.save_entity_name = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // entity_name
            // 
            this.entity_name.Location = new System.Drawing.Point(14, 28);
            this.entity_name.Name = "entity_name";
            this.entity_name.Size = new System.Drawing.Size(731, 20);
            this.entity_name.TabIndex = 0;
            // 
            // save_entity_name
            // 
            this.save_entity_name.Location = new System.Drawing.Point(599, 52);
            this.save_entity_name.Name = "save_entity_name";
            this.save_entity_name.Size = new System.Drawing.Size(147, 23);
            this.save_entity_name.TabIndex = 4;
            this.save_entity_name.Text = "Save";
            this.save_entity_name.UseVisualStyleBackColor = true;
            this.save_entity_name.Click += new System.EventHandler(this.save_entity_name_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(87, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Composite Name";
            // 
            // RenameGeneric
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(758, 87);
            this.Controls.Add(this.entity_name);
            this.Controls.Add(this.save_entity_name);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "RenameGeneric";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Rename Composite";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.TextBox entity_name;
        private System.Windows.Forms.Button save_entity_name;
        private System.Windows.Forms.Label label1;
    }
}

namespace OpenCAGE
{
    partial class InstanceSelection
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(InstanceSelection));
            this.addInstance = new System.Windows.Forms.Button();
            this.instances = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // addInstance
            // 
            this.addInstance.Location = new System.Drawing.Point(764, 11);
            this.addInstance.Name = "addInstance";
            this.addInstance.Size = new System.Drawing.Size(88, 23);
            this.addInstance.TabIndex = 22;
            this.addInstance.Text = "Add";
            this.addInstance.UseVisualStyleBackColor = true;
            this.addInstance.Click += new System.EventHandler(this.addCharacter_Click);
            // 
            // instances
            // 
            this.instances.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.instances.FormattingEnabled = true;
            this.instances.Location = new System.Drawing.Point(12, 12);
            this.instances.Name = "instances";
            this.instances.Size = new System.Drawing.Size(746, 21);
            this.instances.TabIndex = 21;
            // 
            // InstanceSelection
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(864, 45);
            this.Controls.Add(this.addInstance);
            this.Controls.Add(this.instances);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "InstanceSelection";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Add New Instance Reference";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button addInstance;
        private System.Windows.Forms.ComboBox instances;
    }
}

namespace CommandsEditor
{
    partial class AddOrEditResource
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddOrEditResource));
            this.resource_panel = new System.Windows.Forms.Panel();
            this.addResource = new System.Windows.Forms.Button();
            this.deleteResource = new System.Windows.Forms.Button();
            this.resourceType = new System.Windows.Forms.ComboBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // resource_panel
            // 
            this.resource_panel.AutoScroll = true;
            this.resource_panel.Location = new System.Drawing.Point(12, 12);
            this.resource_panel.Name = "resource_panel";
            this.resource_panel.Size = new System.Drawing.Size(915, 538);
            this.resource_panel.TabIndex = 1;
            // 
            // addResource
            // 
            this.addResource.Location = new System.Drawing.Point(6, 45);
            this.addResource.Name = "addResource";
            this.addResource.Size = new System.Drawing.Size(148, 23);
            this.addResource.TabIndex = 4;
            this.addResource.Text = "Add New Reference";
            this.addResource.UseVisualStyleBackColor = true;
            this.addResource.Click += new System.EventHandler(this.addResource_Click);
            // 
            // deleteResource
            // 
            this.deleteResource.Location = new System.Drawing.Point(160, 45);
            this.deleteResource.Name = "deleteResource";
            this.deleteResource.Size = new System.Drawing.Size(148, 23);
            this.deleteResource.TabIndex = 5;
            this.deleteResource.Text = "Delete Existing Reference";
            this.deleteResource.UseVisualStyleBackColor = true;
            this.deleteResource.Click += new System.EventHandler(this.deleteResource_Click);
            // 
            // resourceType
            // 
            this.resourceType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.resourceType.FormattingEnabled = true;
            this.resourceType.Items.AddRange(new object[] {
            "RENDERABLE_INSTANCE",
            "COLLISION_MAPPING",
            "ANIMATED_MODEL",
            "DYNAMIC_PHYSICS_SYSTEM"});
            this.resourceType.Location = new System.Drawing.Point(6, 19);
            this.resourceType.Name = "resourceType";
            this.resourceType.Size = new System.Drawing.Size(302, 21);
            this.resourceType.TabIndex = 6;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.resourceType);
            this.groupBox1.Controls.Add(this.addResource);
            this.groupBox1.Controls.Add(this.deleteResource);
            this.groupBox1.Location = new System.Drawing.Point(12, 556);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(315, 75);
            this.groupBox1.TabIndex = 7;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Modify Resource References";
            // 
            // AddOrEditResource
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(936, 643);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.resource_panel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "AddOrEditResource";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Resource Reference Editor";
            this.groupBox1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel resource_panel;
        private System.Windows.Forms.Button addResource;
        private System.Windows.Forms.Button deleteResource;
        private System.Windows.Forms.ComboBox resourceType;
        private System.Windows.Forms.GroupBox groupBox1;
    }
}

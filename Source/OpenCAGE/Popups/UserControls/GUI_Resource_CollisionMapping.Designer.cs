namespace OpenCAGE.Popups.UserControls
{
    partial class GUI_Resource_CollisionMapping
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
            this.labelHavok = new System.Windows.Forms.Label();
            this.havokName = new System.Windows.Forms.TextBox();
            this.btnSetHavok = new System.Windows.Forms.Button();
            this.btnClearHavok = new System.Windows.Forms.Button();
            this.labelMaterial = new System.Windows.Forms.Label();
            this.materialName = new System.Windows.Forms.TextBox();
            this.btnSetMaterial = new System.Windows.Forms.Button();
            this.labelMaterialMapping = new System.Windows.Forms.Label();
            this.materialMappingName = new System.Windows.Forms.TextBox();
            this.btnSetMaterialMapping = new System.Windows.Forms.Button();
            this.btnClearMaterialMapping = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.labelHavok);
            this.groupBox1.Controls.Add(this.havokName);
            this.groupBox1.Controls.Add(this.btnSetHavok);
            this.groupBox1.Controls.Add(this.btnClearHavok);
            this.groupBox1.Controls.Add(this.labelMaterial);
            this.groupBox1.Controls.Add(this.materialName);
            this.groupBox1.Controls.Add(this.btnSetMaterial);
            this.groupBox1.Controls.Add(this.labelMaterialMapping);
            this.groupBox1.Controls.Add(this.materialMappingName);
            this.groupBox1.Controls.Add(this.btnSetMaterialMapping);
            this.groupBox1.Controls.Add(this.btnClearMaterialMapping);
            this.groupBox1.Location = new System.Drawing.Point(3, 3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(832, 118);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Collision Mapping";
            // 
            // labelHavok
            // 
            this.labelHavok.Location = new System.Drawing.Point(6, 22);
            this.labelHavok.Name = "labelHavok";
            this.labelHavok.Size = new System.Drawing.Size(118, 20);
            this.labelHavok.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.labelHavok.TabIndex = 0;
            this.labelHavok.Text = "Havok compound:";
            // 
            // havokName
            // 
            this.havokName.Location = new System.Drawing.Point(130, 22);
            this.havokName.Name = "havokName";
            this.havokName.ReadOnly = true;
            this.havokName.Size = new System.Drawing.Size(459, 20);
            this.havokName.TabIndex = 1;
            // 
            // btnSetHavok
            // 
            this.btnSetHavok.Location = new System.Drawing.Point(595, 21);
            this.btnSetHavok.Name = "btnSetHavok";
            this.btnSetHavok.Size = new System.Drawing.Size(110, 23);
            this.btnSetHavok.TabIndex = 2;
            this.btnSetHavok.Text = "Set Havok...";
            this.btnSetHavok.UseVisualStyleBackColor = true;
            this.btnSetHavok.Click += new System.EventHandler(this.btnSetHavok_Click);
            // 
            // btnClearHavok
            // 
            this.btnClearHavok.Location = new System.Drawing.Point(711, 21);
            this.btnClearHavok.Name = "btnClearHavok";
            this.btnClearHavok.Size = new System.Drawing.Size(75, 23);
            this.btnClearHavok.TabIndex = 3;
            this.btnClearHavok.Text = "Clear";
            this.btnClearHavok.UseVisualStyleBackColor = true;
            this.btnClearHavok.Click += new System.EventHandler(this.btnClearHavok_Click);
            // 
            // labelMaterial
            // 
            this.labelMaterial.Location = new System.Drawing.Point(6, 51);
            this.labelMaterial.Name = "labelMaterial";
            this.labelMaterial.Size = new System.Drawing.Size(118, 20);
            this.labelMaterial.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.labelMaterial.TabIndex = 4;
            this.labelMaterial.Text = "Material:";
            // 
            // materialName
            // 
            this.materialName.Location = new System.Drawing.Point(130, 51);
            this.materialName.Name = "materialName";
            this.materialName.ReadOnly = true;
            this.materialName.Size = new System.Drawing.Size(459, 20);
            this.materialName.TabIndex = 5;
            // 
            // btnSetMaterial
            // 
            this.btnSetMaterial.Location = new System.Drawing.Point(595, 50);
            this.btnSetMaterial.Name = "btnSetMaterial";
            this.btnSetMaterial.Size = new System.Drawing.Size(110, 23);
            this.btnSetMaterial.TabIndex = 6;
            this.btnSetMaterial.Text = "Set Material...";
            this.btnSetMaterial.UseVisualStyleBackColor = true;
            this.btnSetMaterial.Click += new System.EventHandler(this.btnSetMaterial_Click);
            // 
            // labelMaterialMapping
            // 
            this.labelMaterialMapping.Location = new System.Drawing.Point(6, 80);
            this.labelMaterialMapping.Name = "labelMaterialMapping";
            this.labelMaterialMapping.Size = new System.Drawing.Size(118, 20);
            this.labelMaterialMapping.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.labelMaterialMapping.TabIndex = 7;
            this.labelMaterialMapping.Text = "Material Mapping:";
            // 
            // materialMappingName
            // 
            this.materialMappingName.Location = new System.Drawing.Point(130, 80);
            this.materialMappingName.Name = "materialMappingName";
            this.materialMappingName.ReadOnly = true;
            this.materialMappingName.Size = new System.Drawing.Size(459, 20);
            this.materialMappingName.TabIndex = 8;
            // 
            // btnSetMaterialMapping
            // 
            this.btnSetMaterialMapping.Location = new System.Drawing.Point(595, 79);
            this.btnSetMaterialMapping.Name = "btnSetMaterialMapping";
            this.btnSetMaterialMapping.Size = new System.Drawing.Size(110, 23);
            this.btnSetMaterialMapping.TabIndex = 9;
            this.btnSetMaterialMapping.Text = "Set Mapping...";
            this.btnSetMaterialMapping.UseVisualStyleBackColor = true;
            this.btnSetMaterialMapping.Click += new System.EventHandler(this.btnSetMaterialMapping_Click);
            // 
            // btnClearMaterialMapping
            // 
            this.btnClearMaterialMapping.Location = new System.Drawing.Point(711, 79);
            this.btnClearMaterialMapping.Name = "btnClearMaterialMapping";
            this.btnClearMaterialMapping.Size = new System.Drawing.Size(75, 23);
            this.btnClearMaterialMapping.TabIndex = 10;
            this.btnClearMaterialMapping.Text = "Clear";
            this.btnClearMaterialMapping.UseVisualStyleBackColor = true;
            this.btnClearMaterialMapping.Click += new System.EventHandler(this.btnClearMaterialMapping_Click);
            // 
            // GUI_Resource_CollisionMapping
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBox1);
            this.Name = "GUI_Resource_CollisionMapping";
            this.Size = new System.Drawing.Size(838, 124);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label labelHavok;
        private System.Windows.Forms.TextBox havokName;
        private System.Windows.Forms.Button btnSetHavok;
        private System.Windows.Forms.Button btnClearHavok;
        private System.Windows.Forms.Label labelMaterial;
        private System.Windows.Forms.TextBox materialName;
        private System.Windows.Forms.Button btnSetMaterial;
        private System.Windows.Forms.Label labelMaterialMapping;
        private System.Windows.Forms.TextBox materialMappingName;
        private System.Windows.Forms.Button btnSetMaterialMapping;
        private System.Windows.Forms.Button btnClearMaterialMapping;
    }
}

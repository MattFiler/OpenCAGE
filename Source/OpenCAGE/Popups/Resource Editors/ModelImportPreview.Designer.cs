namespace AlienPAK
{
    partial class ModelImportPreview
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ModelImportPreview));
            this.hierarchyTree = new System.Windows.Forms.TreeView();
            this.previewHost = new System.Windows.Forms.Integration.ElementHost();
            this.modelImportPreviewWPF1 = new AlienPAK.ModelImportPreviewWPF();
            this.importBtn = new System.Windows.Forms.Button();
            this.cancelBtn = new System.Windows.Forms.Button();
            this.pickMaterialBtn = new System.Windows.Forms.Button();
            this.panelButtons = new System.Windows.Forms.Panel();
            this.panelButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // hierarchyTree
            // 
            this.hierarchyTree.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.hierarchyTree.Dock = System.Windows.Forms.DockStyle.Left;
            this.hierarchyTree.Location = new System.Drawing.Point(0, 0);
            this.hierarchyTree.Name = "hierarchyTree";
            this.hierarchyTree.Size = new System.Drawing.Size(320, 450);
            this.hierarchyTree.TabIndex = 0;
            // 
            // previewHost
            // 
            this.previewHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.previewHost.Location = new System.Drawing.Point(320, 0);
            this.previewHost.Name = "previewHost";
            this.previewHost.Size = new System.Drawing.Size(480, 450);
            this.previewHost.TabIndex = 1;
            this.previewHost.Child = this.modelImportPreviewWPF1;
            // 
            // importBtn
            // 
            this.importBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.importBtn.Location = new System.Drawing.Point(538, 5);
            this.importBtn.Name = "importBtn";
            this.importBtn.Size = new System.Drawing.Size(120, 28);
            this.importBtn.TabIndex = 0;
            this.importBtn.Text = "Import";
            this.importBtn.UseVisualStyleBackColor = true;
            // 
            // cancelBtn
            // 
            this.cancelBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelBtn.Location = new System.Drawing.Point(668, 5);
            this.cancelBtn.Name = "cancelBtn";
            this.cancelBtn.Size = new System.Drawing.Size(120, 28);
            this.cancelBtn.TabIndex = 1;
            this.cancelBtn.Text = "Cancel";
            this.cancelBtn.UseVisualStyleBackColor = true;
            // 
            // pickMaterialBtn
            // 
            this.pickMaterialBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.pickMaterialBtn.Location = new System.Drawing.Point(12, 5);
            this.pickMaterialBtn.Name = "pickMaterialBtn";
            this.pickMaterialBtn.Size = new System.Drawing.Size(308, 28);
            this.pickMaterialBtn.TabIndex = 2;
            this.pickMaterialBtn.Text = "Pick material for selected";
            this.pickMaterialBtn.UseVisualStyleBackColor = true;
            // 
            // panelButtons
            // 
            this.panelButtons.Controls.Add(this.pickMaterialBtn);
            this.panelButtons.Controls.Add(this.importBtn);
            this.panelButtons.Controls.Add(this.cancelBtn);
            this.panelButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelButtons.Location = new System.Drawing.Point(0, 450);
            this.panelButtons.Name = "panelButtons";
            this.panelButtons.Size = new System.Drawing.Size(800, 45);
            this.panelButtons.TabIndex = 2;
            // 
            // ModelImportPreview
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 495);
            this.Controls.Add(this.previewHost);
            this.Controls.Add(this.hierarchyTree);
            this.Controls.Add(this.panelButtons);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.MinimumSize = new System.Drawing.Size(600, 400);
            this.Name = "ModelImportPreview";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Model import preview";
            this.panelButtons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TreeView hierarchyTree;
        private System.Windows.Forms.Integration.ElementHost previewHost;
        private System.Windows.Forms.Button importBtn;
        private System.Windows.Forms.Button cancelBtn;
        private System.Windows.Forms.Button pickMaterialBtn;
        private System.Windows.Forms.Panel panelButtons;
        private ModelImportPreviewWPF modelImportPreviewWPF1;
    }
}

namespace CommandsEditor
{
    partial class EditMaterial
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.duplicateMaterialBtn = new System.Windows.Forms.Button();
            this.materialList = new System.Windows.Forms.ListView();
            this.materialSearchClearButton = new System.Windows.Forms.Button();
            this.materialSearchButton = new System.Windows.Forms.Button();
            this.materialSearchTextBox = new System.Windows.Forms.TextBox();
            this.selectMaterialBtn = new System.Windows.Forms.Button();
            this.elementHost1 = new System.Windows.Forms.Integration.ElementHost();
            this.MaterialInfoWPF1 = new MaterialInfoWPF();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.duplicateMaterialBtn);
            this.splitContainer1.Panel1.Controls.Add(this.materialList);
            this.splitContainer1.Panel1.Controls.Add(this.materialSearchClearButton);
            this.splitContainer1.Panel1.Controls.Add(this.materialSearchButton);
            this.splitContainer1.Panel1.Controls.Add(this.materialSearchTextBox);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.selectMaterialBtn);
            this.splitContainer1.Panel2.Controls.Add(this.elementHost1);
            this.splitContainer1.Size = new System.Drawing.Size(900, 650);
            this.splitContainer1.SplitterDistance = 400;
            this.splitContainer1.TabIndex = 0;
            // 
            // duplicateMaterialBtn
            // 
            this.duplicateMaterialBtn.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.duplicateMaterialBtn.Location = new System.Drawing.Point(3, 615);
            this.duplicateMaterialBtn.Name = "duplicateMaterialBtn";
            this.duplicateMaterialBtn.Size = new System.Drawing.Size(394, 27);
            this.duplicateMaterialBtn.TabIndex = 27;
            this.duplicateMaterialBtn.Text = "Duplicate Selected Material";
            this.duplicateMaterialBtn.UseVisualStyleBackColor = true;
            this.duplicateMaterialBtn.Click += new System.EventHandler(this.duplicateMaterial_Click);
            // 
            // materialList
            // 
            this.materialList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.materialList.FullRowSelect = true;
            this.materialList.HideSelection = false;
            this.materialList.LabelWrap = false;
            this.materialList.Location = new System.Drawing.Point(3, 35);
            this.materialList.MultiSelect = false;
            this.materialList.Name = "materialList";
            this.materialList.Size = new System.Drawing.Size(394, 574);
            this.materialList.TabIndex = 21;
            this.materialList.UseCompatibleStateImageBehavior = false;
            this.materialList.View = System.Windows.Forms.View.Details;
            this.materialList.SelectedIndexChanged += new System.EventHandler(this.materialList_SelectedIndexChanged);
            // 
            // materialSearchClearButton
            // 
            this.materialSearchClearButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.materialSearchClearButton.Location = new System.Drawing.Point(321, 6);
            this.materialSearchClearButton.Name = "materialSearchClearButton";
            this.materialSearchClearButton.Size = new System.Drawing.Size(76, 23);
            this.materialSearchClearButton.TabIndex = 26;
            this.materialSearchClearButton.Text = "Clear";
            this.materialSearchClearButton.UseVisualStyleBackColor = true;
            this.materialSearchClearButton.Click += new System.EventHandler(this.materialSearchClearButton_Click);
            // 
            // materialSearchButton
            // 
            this.materialSearchButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.materialSearchButton.Location = new System.Drawing.Point(245, 6);
            this.materialSearchButton.Name = "materialSearchButton";
            this.materialSearchButton.Size = new System.Drawing.Size(70, 23);
            this.materialSearchButton.TabIndex = 25;
            this.materialSearchButton.Text = "Search";
            this.materialSearchButton.UseVisualStyleBackColor = true;
            this.materialSearchButton.Click += new System.EventHandler(this.materialSearchButton_Click);
            // 
            // materialSearchTextBox
            // 
            this.materialSearchTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.materialSearchTextBox.Location = new System.Drawing.Point(3, 8);
            this.materialSearchTextBox.Name = "materialSearchTextBox";
            this.materialSearchTextBox.Size = new System.Drawing.Size(236, 20);
            this.materialSearchTextBox.TabIndex = 24;
            this.materialSearchTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.materialSearchTextBox_KeyDown);
            // 
            // selectMaterialBtn
            // 
            this.selectMaterialBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.selectMaterialBtn.Location = new System.Drawing.Point(353, 608);
            this.selectMaterialBtn.Name = "selectMaterialBtn";
            this.selectMaterialBtn.Size = new System.Drawing.Size(140, 30);
            this.selectMaterialBtn.TabIndex = 22;
            this.selectMaterialBtn.Text = "Use This Material";
            this.selectMaterialBtn.UseVisualStyleBackColor = true;
            this.selectMaterialBtn.Click += new System.EventHandler(this.selectMaterial_Click);
            // 
            // elementHost1
            // 
            this.elementHost1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.elementHost1.Location = new System.Drawing.Point(0, 0);
            this.elementHost1.Name = "elementHost1";
            this.elementHost1.Size = new System.Drawing.Size(496, 650);
            this.elementHost1.TabIndex = 20;
            this.elementHost1.Text = "elementHost1";
            this.elementHost1.Child = this.MaterialInfoWPF1;
            // 
            // EditMaterial
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 650);
            this.Controls.Add(this.splitContainer1);
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.MinimumSize = new System.Drawing.Size(700, 500);
            this.Name = "EditMaterial";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Material Editor";
            this.Load += new System.EventHandler(this.EditMaterial_Load);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Button duplicateMaterialBtn;
        private System.Windows.Forms.ListView materialList;
        private System.Windows.Forms.Button materialSearchClearButton;
        private System.Windows.Forms.Button materialSearchButton;
        private System.Windows.Forms.TextBox materialSearchTextBox;
        private System.Windows.Forms.Integration.ElementHost elementHost1;
        private MaterialInfoWPF MaterialInfoWPF1;
        private System.Windows.Forms.Button selectMaterialBtn;
    }
}

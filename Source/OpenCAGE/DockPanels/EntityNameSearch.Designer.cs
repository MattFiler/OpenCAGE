namespace OpenCAGE.DockPanels
{
    partial class EntityNameSearch
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager clearBtnResources = new System.ComponentModel.ComponentResourceManager(typeof(OpenCAGE.Popups.UserControls.CompositeEntityList));
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OpenCAGE.GlobalEntitySearcher));
            this.searchHeaderPanel = new System.Windows.Forms.Panel();
            this.scopeSettingsBtn = new System.Windows.Forms.Button();
            this.clearSearchBtn = new System.Windows.Forms.Button();
            this.nameSearchBox = new System.Windows.Forms.TextBox();
            this.entityList = new System.Windows.Forms.ListView();
            this.entityNameColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.entityTypeColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.entityListIcons = new System.Windows.Forms.ImageList(this.components);
            this.searchHeaderPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // searchHeaderPanel
            // 
            this.searchHeaderPanel.Controls.Add(this.scopeSettingsBtn);
            this.searchHeaderPanel.Controls.Add(this.clearSearchBtn);
            this.searchHeaderPanel.Controls.Add(this.nameSearchBox);
            this.searchHeaderPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.searchHeaderPanel.Name = "searchHeaderPanel";
            this.searchHeaderPanel.Size = new System.Drawing.Size(365, 24);
            this.searchHeaderPanel.TabIndex = 0;
            // 
            // scopeSettingsBtn
            // 
            this.scopeSettingsBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.scopeSettingsBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.scopeSettingsBtn.Location = new System.Drawing.Point(321, 2);
            this.scopeSettingsBtn.Name = "scopeSettingsBtn";
            this.scopeSettingsBtn.Size = new System.Drawing.Size(20, 20);
            this.scopeSettingsBtn.TabIndex = 2;
            this.scopeSettingsBtn.UseVisualStyleBackColor = true;
            // 
            // clearSearchBtn
            // 
            this.clearSearchBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.clearSearchBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.clearSearchBtn.Image = ((System.Drawing.Image)(clearBtnResources.GetObject("clearSearchBtn.Image")));
            this.clearSearchBtn.Location = new System.Drawing.Point(345, 2);
            this.clearSearchBtn.Name = "clearSearchBtn";
            this.clearSearchBtn.Size = new System.Drawing.Size(20, 20);
            this.clearSearchBtn.TabIndex = 1;
            this.clearSearchBtn.UseVisualStyleBackColor = true;
            this.clearSearchBtn.Visible = false;
            // 
            // nameSearchBox
            // 
            this.nameSearchBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.nameSearchBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.nameSearchBox.Location = new System.Drawing.Point(0, 2);
            this.nameSearchBox.Name = "nameSearchBox";
            this.nameSearchBox.Size = new System.Drawing.Size(315, 20);
            this.nameSearchBox.TabIndex = 0;
            // 
            // entityList
            // 
            this.entityList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.entityList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.entityNameColumn,
            this.entityTypeColumn});
            this.entityList.FullRowSelect = true;
            this.entityList.HideSelection = false;
            this.entityList.MultiSelect = false;
            this.entityList.Name = "entityList";
            this.entityList.SmallImageList = this.entityListIcons;
            this.entityList.TabIndex = 1;
            this.entityList.UseCompatibleStateImageBehavior = false;
            this.entityList.View = System.Windows.Forms.View.Details;
            // 
            // entityNameColumn
            // 
            this.entityNameColumn.Text = "Name";
            this.entityNameColumn.Width = 180;
            // 
            // entityTypeColumn
            // 
            this.entityTypeColumn.Text = "Type";
            this.entityTypeColumn.Width = 120;
            // 
            // entityListIcons
            // 
            this.entityListIcons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("entityListIcons.ImageStream")));
            this.entityListIcons.TransparentColor = System.Drawing.Color.Transparent;
            this.entityListIcons.Images.SetKeyName(0, "AnimatorController Icon.png");
            this.entityListIcons.Images.SetKeyName(1, "d_ScriptableObject Icon braces only.png");
            this.entityListIcons.Images.SetKeyName(2, "d_PrefabVariant Icon.png");
            this.entityListIcons.Images.SetKeyName(3, "d_ScriptableObject Icon.png");
            this.entityListIcons.Images.SetKeyName(4, "AreaEffector2D Icon.ico");
            this.entityListIcons.Images.SetKeyName(5, "variable left.png");
            this.entityListIcons.Images.SetKeyName(6, "variable right.png");
            // 
            // EntityNameSearch
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(381, 292);
            this.CloseButtonVisible = false;
            this.Controls.Add(this.entityList);
            this.Controls.Add(this.searchHeaderPanel);
            this.Padding = new System.Windows.Forms.Padding(8);
            this.DockAreas = ((WeifenLuo.WinFormsUI.Docking.DockAreas)((WeifenLuo.WinFormsUI.Docking.DockAreas.Float | WeifenLuo.WinFormsUI.Docking.DockAreas.DockLeft)));
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.Name = "EntityNameSearch";
            this.ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.DockLeft;
            this.Text = "Search by Name";
            this.searchHeaderPanel.ResumeLayout(false);
            this.searchHeaderPanel.PerformLayout();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Panel searchHeaderPanel;
        private System.Windows.Forms.TextBox nameSearchBox;
        private System.Windows.Forms.Button clearSearchBtn;
        private System.Windows.Forms.Button scopeSettingsBtn;
        private System.Windows.Forms.ListView entityList;
        private System.Windows.Forms.ColumnHeader entityNameColumn;
        private System.Windows.Forms.ColumnHeader entityTypeColumn;
        private System.Windows.Forms.ImageList entityListIcons;
    }
}

namespace OpenCAGE.DockPanels
{
    partial class EntitySearch
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
            this.modeCombo = new System.Windows.Forms.ComboBox();
            this.scopeSettingsBtn = new System.Windows.Forms.Button();
            this.nameSearchBox = new System.Windows.Forms.TextBox();
            this.clearSearchBtn = new System.Windows.Forms.Button();
            this.functionTypeCombo = new System.Windows.Forms.ComboBox();
            this.browseFunctionButton = new System.Windows.Forms.Button();
            this.compositeNameBox = new System.Windows.Forms.TextBox();
            this.browseCompositeButton = new System.Windows.Forms.Button();
            this.entityList = new System.Windows.Forms.ListView();
            this.entityNameColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.entityTypeColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.entityListIcons = new System.Windows.Forms.ImageList(this.components);
            this.searchHeaderPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // searchHeaderPanel
            // 
            this.searchHeaderPanel.Controls.Add(this.modeCombo);
            this.searchHeaderPanel.Controls.Add(this.scopeSettingsBtn);
            this.searchHeaderPanel.Controls.Add(this.nameSearchBox);
            this.searchHeaderPanel.Controls.Add(this.clearSearchBtn);
            this.searchHeaderPanel.Controls.Add(this.functionTypeCombo);
            this.searchHeaderPanel.Controls.Add(this.browseFunctionButton);
            this.searchHeaderPanel.Controls.Add(this.compositeNameBox);
            this.searchHeaderPanel.Controls.Add(this.browseCompositeButton);
            this.searchHeaderPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.searchHeaderPanel.Name = "searchHeaderPanel";
            this.searchHeaderPanel.Size = new System.Drawing.Size(365, 48);
            this.searchHeaderPanel.TabIndex = 0;
            // 
            // modeCombo
            // 
            this.modeCombo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.modeCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.modeCombo.FormattingEnabled = true;
            this.modeCombo.Location = new System.Drawing.Point(0, 2);
            this.modeCombo.Name = "modeCombo";
            this.modeCombo.Size = new System.Drawing.Size(341, 21);
            this.modeCombo.TabIndex = 0;
            // 
            // scopeSettingsBtn
            // 
            this.scopeSettingsBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.scopeSettingsBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.scopeSettingsBtn.Location = new System.Drawing.Point(345, 2);
            this.scopeSettingsBtn.Name = "scopeSettingsBtn";
            this.scopeSettingsBtn.Size = new System.Drawing.Size(20, 20);
            this.scopeSettingsBtn.TabIndex = 1;
            this.scopeSettingsBtn.UseVisualStyleBackColor = true;
            // 
            // nameSearchBox
            // 
            this.nameSearchBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.nameSearchBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.nameSearchBox.Location = new System.Drawing.Point(0, 26);
            this.nameSearchBox.Name = "nameSearchBox";
            this.nameSearchBox.Size = new System.Drawing.Size(341, 20);
            this.nameSearchBox.TabIndex = 2;
            // 
            // clearSearchBtn
            // 
            this.clearSearchBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.clearSearchBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.clearSearchBtn.Image = ((System.Drawing.Image)(clearBtnResources.GetObject("clearSearchBtn.Image")));
            this.clearSearchBtn.Location = new System.Drawing.Point(345, 26);
            this.clearSearchBtn.Name = "clearSearchBtn";
            this.clearSearchBtn.Size = new System.Drawing.Size(20, 20);
            this.clearSearchBtn.TabIndex = 3;
            this.clearSearchBtn.UseVisualStyleBackColor = true;
            this.clearSearchBtn.Visible = false;
            // 
            // functionTypeCombo
            // 
            this.functionTypeCombo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.functionTypeCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.functionTypeCombo.FormattingEnabled = true;
            this.functionTypeCombo.Location = new System.Drawing.Point(0, 26);
            this.functionTypeCombo.Name = "functionTypeCombo";
            this.functionTypeCombo.Size = new System.Drawing.Size(341, 21);
            this.functionTypeCombo.TabIndex = 4;
            this.functionTypeCombo.Visible = false;
            // 
            // browseFunctionButton
            // 
            this.browseFunctionButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.browseFunctionButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.browseFunctionButton.Location = new System.Drawing.Point(345, 26);
            this.browseFunctionButton.Name = "browseFunctionButton";
            this.browseFunctionButton.Size = new System.Drawing.Size(20, 20);
            this.browseFunctionButton.TabIndex = 5;
            this.browseFunctionButton.UseVisualStyleBackColor = true;
            this.browseFunctionButton.Visible = false;
            // 
            // compositeNameBox
            // 
            this.compositeNameBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.compositeNameBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.compositeNameBox.Location = new System.Drawing.Point(0, 26);
            this.compositeNameBox.Name = "compositeNameBox";
            this.compositeNameBox.ReadOnly = true;
            this.compositeNameBox.Size = new System.Drawing.Size(341, 20);
            this.compositeNameBox.TabIndex = 6;
            this.compositeNameBox.Visible = false;
            // 
            // browseCompositeButton
            // 
            this.browseCompositeButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.browseCompositeButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.browseCompositeButton.Location = new System.Drawing.Point(345, 26);
            this.browseCompositeButton.Name = "browseCompositeButton";
            this.browseCompositeButton.Size = new System.Drawing.Size(20, 20);
            this.browseCompositeButton.TabIndex = 7;
            this.browseCompositeButton.UseVisualStyleBackColor = true;
            this.browseCompositeButton.Visible = false;
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
            // EntitySearch
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
            this.Name = "EntitySearch";
            this.ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.DockLeft;
            this.Text = "Search";
            this.searchHeaderPanel.ResumeLayout(false);
            this.searchHeaderPanel.PerformLayout();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Panel searchHeaderPanel;
        private System.Windows.Forms.ComboBox modeCombo;
        private System.Windows.Forms.Button scopeSettingsBtn;
        private System.Windows.Forms.TextBox nameSearchBox;
        private System.Windows.Forms.Button clearSearchBtn;
        private System.Windows.Forms.ComboBox functionTypeCombo;
        private System.Windows.Forms.Button browseFunctionButton;
        private System.Windows.Forms.TextBox compositeNameBox;
        private System.Windows.Forms.Button browseCompositeButton;
        private System.Windows.Forms.ListView entityList;
        private System.Windows.Forms.ColumnHeader entityNameColumn;
        private System.Windows.Forms.ColumnHeader entityTypeColumn;
        private System.Windows.Forms.ImageList entityListIcons;
    }
}

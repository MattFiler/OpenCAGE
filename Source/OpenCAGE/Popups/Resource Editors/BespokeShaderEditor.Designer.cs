namespace OpenCAGE
{
    partial class BespokeShaderEditor
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
            this.components = new System.ComponentModel.Container();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.shaderList = new System.Windows.Forms.ListView();
            this.colIndex = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colShader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colStages = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colChanged = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.searchPanel = new System.Windows.Forms.Panel();
            this.searchTxt = new System.Windows.Forms.TextBox();
            this.searchLabel = new System.Windows.Forms.Label();
            this.tabs = new System.Windows.Forms.TabControl();
            this.propertiesTab = new System.Windows.Forms.TabPage();
            this.propertiesList = new System.Windows.Forms.ListView();
            this.colProperty = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colValue = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.statusLabel = new System.Windows.Forms.Label();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.searchPanel.SuspendLayout();
            this.tabs.SuspendLayout();
            this.propertiesTab.SuspendLayout();
            this.SuspendLayout();
            //
            // splitContainer
            //
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer.Location = new System.Drawing.Point(0, 0);
            this.splitContainer.Name = "splitContainer";
            //
            // splitContainer.Panel1
            //
            this.splitContainer.Panel1.Controls.Add(this.shaderList);
            this.splitContainer.Panel1.Controls.Add(this.searchPanel);
            //
            // splitContainer.Panel2
            //
            this.splitContainer.Panel2.Controls.Add(this.tabs);
            this.splitContainer.Size = new System.Drawing.Size(1280, 758);
            this.splitContainer.SplitterDistance = 430;
            this.splitContainer.Panel1MinSize = 260;
            this.splitContainer.TabIndex = 0;
            //
            // shaderList
            //
            this.shaderList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colIndex,
            this.colShader,
            this.colStages,
            this.colChanged});
            this.shaderList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.shaderList.FullRowSelect = true;
            this.shaderList.HideSelection = false;
            this.shaderList.Location = new System.Drawing.Point(0, 30);
            this.shaderList.MultiSelect = false;
            this.shaderList.Name = "shaderList";
            this.shaderList.Size = new System.Drawing.Size(430, 728);
            this.shaderList.TabIndex = 1;
            this.shaderList.UseCompatibleStateImageBehavior = false;
            this.shaderList.View = System.Windows.Forms.View.Details;
            this.shaderList.SelectedIndexChanged += new System.EventHandler(this.shaderList_SelectedIndexChanged);
            //
            // colIndex
            //
            this.colIndex.Text = "#";
            this.colIndex.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.colIndex.Width = 40;
            //
            // colShader
            //
            this.colShader.Text = "Shader";
            this.colShader.Width = 230;
            //
            // colStages
            //
            this.colStages.Text = "Stages";
            this.colStages.Width = 70;
            //
            // colChanged
            //
            this.colChanged.Text = "Changed";
            this.colChanged.Width = 60;
            //
            // searchPanel
            //
            this.searchPanel.Controls.Add(this.searchTxt);
            this.searchPanel.Controls.Add(this.searchLabel);
            this.searchPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.searchPanel.Location = new System.Drawing.Point(0, 0);
            this.searchPanel.Name = "searchPanel";
            this.searchPanel.Size = new System.Drawing.Size(430, 30);
            this.searchPanel.TabIndex = 0;
            //
            // searchTxt
            //
            this.searchTxt.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.searchTxt.Location = new System.Drawing.Point(56, 5);
            this.searchTxt.Name = "searchTxt";
            this.searchTxt.Size = new System.Drawing.Size(370, 20);
            this.searchTxt.TabIndex = 1;
            this.toolTip.SetToolTip(this.searchTxt, "Show only the shaders whose names have this in them");
            this.searchTxt.TextChanged += new System.EventHandler(this.searchTxt_TextChanged);
            //
            // searchLabel
            //
            this.searchLabel.AutoSize = true;
            this.searchLabel.Location = new System.Drawing.Point(6, 8);
            this.searchLabel.Name = "searchLabel";
            this.searchLabel.Size = new System.Drawing.Size(44, 13);
            this.searchLabel.TabIndex = 0;
            this.searchLabel.Text = "Search:";
            //
            // tabs
            //
            this.tabs.Controls.Add(this.propertiesTab);
            this.tabs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabs.Location = new System.Drawing.Point(0, 0);
            this.tabs.Name = "tabs";
            this.tabs.SelectedIndex = 0;
            this.tabs.Size = new System.Drawing.Size(846, 758);
            this.tabs.TabIndex = 0;
            //
            // propertiesTab
            //
            this.propertiesTab.Controls.Add(this.propertiesList);
            this.propertiesTab.Location = new System.Drawing.Point(4, 22);
            this.propertiesTab.Name = "propertiesTab";
            this.propertiesTab.Size = new System.Drawing.Size(838, 732);
            this.propertiesTab.TabIndex = 0;
            this.propertiesTab.Text = "Properties";
            this.propertiesTab.UseVisualStyleBackColor = true;
            //
            // propertiesList
            //
            this.propertiesList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colProperty,
            this.colValue});
            this.propertiesList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertiesList.FullRowSelect = true;
            this.propertiesList.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.propertiesList.HideSelection = false;
            this.propertiesList.Location = new System.Drawing.Point(0, 0);
            this.propertiesList.Name = "propertiesList";
            this.propertiesList.ShowItemToolTips = true;
            this.propertiesList.Size = new System.Drawing.Size(838, 732);
            this.propertiesList.TabIndex = 0;
            this.propertiesList.UseCompatibleStateImageBehavior = false;
            this.propertiesList.View = System.Windows.Forms.View.Details;
            //
            // colProperty
            //
            this.colProperty.Text = "Property";
            this.colProperty.Width = 230;
            //
            // colValue
            //
            this.colValue.Text = "Value";
            this.colValue.Width = 700;
            //
            // statusLabel
            //
            this.statusLabel.AutoEllipsis = true;
            this.statusLabel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.statusLabel.Location = new System.Drawing.Point(0, 758);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Padding = new System.Windows.Forms.Padding(6, 4, 6, 0);
            this.statusLabel.Size = new System.Drawing.Size(1280, 22);
            this.statusLabel.TabIndex = 1;
            //
            // BespokeShaderEditor
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1280, 780);
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.statusLabel);
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MinimumSize = new System.Drawing.Size(900, 520);
            this.Name = "BespokeShaderEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Bespoke Shader Editor";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.BespokeShaderEditor_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.BespokeShaderEditor_FormClosed);
            this.Load += new System.EventHandler(this.BespokeShaderEditor_Load);
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.searchPanel.ResumeLayout(false);
            this.searchPanel.PerformLayout();
            this.tabs.ResumeLayout(false);
            this.propertiesTab.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.ListView shaderList;
        private System.Windows.Forms.ColumnHeader colIndex;
        private System.Windows.Forms.ColumnHeader colShader;
        private System.Windows.Forms.ColumnHeader colStages;
        private System.Windows.Forms.ColumnHeader colChanged;
        private System.Windows.Forms.Panel searchPanel;
        private System.Windows.Forms.TextBox searchTxt;
        private System.Windows.Forms.Label searchLabel;
        private System.Windows.Forms.TabControl tabs;
        private System.Windows.Forms.TabPage propertiesTab;
        private System.Windows.Forms.ListView propertiesList;
        private System.Windows.Forms.ColumnHeader colProperty;
        private System.Windows.Forms.ColumnHeader colValue;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.ToolTip toolTip;
    }
}

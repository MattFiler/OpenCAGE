namespace OpenCAGE
{
    partial class EditPhysicsSystem
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
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.searchBox = new System.Windows.Forms.TextBox();
            this.systemList = new System.Windows.Forms.ListView();
            this.columnIndex = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnLeaf = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnPath = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.bottomPanel = new System.Windows.Forms.Panel();
            this.statusLabel = new System.Windows.Forms.Label();
            this.selectButton = new System.Windows.Forms.Button();
            this.searchLabel = new System.Windows.Forms.Label();
            this.previewGroup = new System.Windows.Forms.GroupBox();
            this.splitPreview = new System.Windows.Forms.SplitContainer();
            this.modelRendererHost = new System.Windows.Forms.Integration.ElementHost();
            this.bodyList = new System.Windows.Forms.ListView();
            this.columnBodyName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnBodyShape = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnBodyMotion = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnBodyMass = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnBodyFilter = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnBodyRadius = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnBodyDamping = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.bodyDetailLabel = new System.Windows.Forms.Label();
            this.previewStatus = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            this.bottomPanel.SuspendLayout();
            this.previewGroup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitPreview)).BeginInit();
            this.splitPreview.Panel1.SuspendLayout();
            this.splitPreview.Panel2.SuspendLayout();
            this.splitPreview.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitMain
            // 
            this.splitMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.Location = new System.Drawing.Point(0, 0);
            this.splitMain.Name = "splitMain";
            this.splitMain.Panel1.Controls.Add(this.systemList);
            this.splitMain.Panel1.Controls.Add(this.searchBox);
            this.splitMain.Panel1.Controls.Add(this.searchLabel);
            this.splitMain.Panel1.Controls.Add(this.bottomPanel);
            this.splitMain.Panel1MinSize = 280;
            this.splitMain.Panel2.Controls.Add(this.previewGroup);
            this.splitMain.Panel2MinSize = 280;
            this.splitMain.Size = new System.Drawing.Size(1100, 620);
            this.splitMain.SplitterDistance = 420;
            this.splitMain.TabIndex = 0;
            // 
            // searchLabel
            // 
            this.searchLabel.AutoSize = true;
            this.searchLabel.Location = new System.Drawing.Point(12, 15);
            this.searchLabel.Name = "searchLabel";
            this.searchLabel.Size = new System.Drawing.Size(32, 13);
            this.searchLabel.TabIndex = 0;
            this.searchLabel.Text = "Filter:";
            // 
            // searchBox
            // 
            this.searchBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.searchBox.Location = new System.Drawing.Point(50, 12);
            this.searchBox.Name = "searchBox";
            this.searchBox.Size = new System.Drawing.Size(355, 20);
            this.searchBox.TabIndex = 1;
            this.searchBox.TextChanged += new System.EventHandler(this.searchBox_TextChanged);
            // 
            // systemList
            // 
            this.systemList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.systemList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnIndex,
            this.columnLeaf,
            this.columnPath});
            this.systemList.FullRowSelect = true;
            this.systemList.GridLines = true;
            this.systemList.HideSelection = false;
            this.systemList.Location = new System.Drawing.Point(12, 42);
            this.systemList.MultiSelect = false;
            this.systemList.Name = "systemList";
            this.systemList.Size = new System.Drawing.Size(393, 530);
            this.systemList.TabIndex = 2;
            this.systemList.UseCompatibleStateImageBehavior = false;
            this.systemList.View = System.Windows.Forms.View.Details;
            this.systemList.SelectedIndexChanged += new System.EventHandler(this.systemList_SelectedIndexChanged);
            this.systemList.DoubleClick += new System.EventHandler(this.systemList_DoubleClick);
            // 
            // columnIndex
            // 
            this.columnIndex.Text = "Index";
            this.columnIndex.Width = 50;
            // 
            // columnLeaf
            // 
            this.columnLeaf.Text = "Name";
            this.columnLeaf.Width = 160;
            // 
            // columnPath
            // 
            this.columnPath.Text = "Path";
            this.columnPath.Width = 160;
            // 
            // bottomPanel
            // 
            this.bottomPanel.Controls.Add(this.statusLabel);
            this.bottomPanel.Controls.Add(this.selectButton);
            this.bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottomPanel.Location = new System.Drawing.Point(0, 582);
            this.bottomPanel.Name = "bottomPanel";
            this.bottomPanel.Size = new System.Drawing.Size(420, 38);
            this.bottomPanel.TabIndex = 3;
            // 
            // statusLabel
            // 
            this.statusLabel.AutoSize = true;
            this.statusLabel.Location = new System.Drawing.Point(12, 12);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(0, 13);
            this.statusLabel.TabIndex = 0;
            // 
            // selectButton
            // 
            this.selectButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.selectButton.Enabled = false;
            this.selectButton.Location = new System.Drawing.Point(282, 7);
            this.selectButton.Name = "selectButton";
            this.selectButton.Size = new System.Drawing.Size(125, 24);
            this.selectButton.TabIndex = 1;
            this.selectButton.Text = "Select System";
            this.selectButton.UseVisualStyleBackColor = true;
            this.selectButton.Click += new System.EventHandler(this.selectButton_Click);
            // 
            // previewGroup
            // 
            this.previewGroup.Controls.Add(this.splitPreview);
            this.previewGroup.Controls.Add(this.previewStatus);
            this.previewGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.previewGroup.Location = new System.Drawing.Point(0, 0);
            this.previewGroup.Name = "previewGroup";
            this.previewGroup.Padding = new System.Windows.Forms.Padding(8);
            this.previewGroup.Size = new System.Drawing.Size(676, 620);
            this.previewGroup.TabIndex = 0;
            this.previewGroup.TabStop = false;
            this.previewGroup.Text = "Preview";
            // 
            // splitPreview
            // 
            this.splitPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitPreview.Location = new System.Drawing.Point(8, 21);
            this.splitPreview.Name = "splitPreview";
            this.splitPreview.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitPreview.Panel1.Controls.Add(this.modelRendererHost);
            this.splitPreview.Panel2.Controls.Add(this.bodyList);
            this.splitPreview.Panel2.Controls.Add(this.bodyDetailLabel);
            this.splitPreview.Panel2MinSize = 120;
            this.splitPreview.Size = new System.Drawing.Size(660, 570);
            this.splitPreview.SplitterDistance = 360;
            this.splitPreview.TabIndex = 0;
            // 
            // modelRendererHost
            // 
            this.modelRendererHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.modelRendererHost.Location = new System.Drawing.Point(0, 0);
            this.modelRendererHost.Name = "modelRendererHost";
            this.modelRendererHost.Size = new System.Drawing.Size(660, 360);
            this.modelRendererHost.TabIndex = 0;
            this.modelRendererHost.Child = null;
            // 
            // bodyDetailLabel
            // 
            this.bodyDetailLabel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bodyDetailLabel.Location = new System.Drawing.Point(0, 176);
            this.bodyDetailLabel.Name = "bodyDetailLabel";
            this.bodyDetailLabel.Padding = new System.Windows.Forms.Padding(4, 2, 4, 2);
            this.bodyDetailLabel.Size = new System.Drawing.Size(660, 30);
            this.bodyDetailLabel.TabIndex = 1;
            this.bodyDetailLabel.Text = "Select a rigid body for details.";
            this.bodyDetailLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // bodyList
            // 
            this.bodyList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnBodyName,
            this.columnBodyShape,
            this.columnBodyMotion,
            this.columnBodyMass,
            this.columnBodyFilter,
            this.columnBodyRadius,
            this.columnBodyDamping});
            this.bodyList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.bodyList.FullRowSelect = true;
            this.bodyList.GridLines = true;
            this.bodyList.HideSelection = false;
            this.bodyList.Location = new System.Drawing.Point(0, 0);
            this.bodyList.MultiSelect = false;
            this.bodyList.Name = "bodyList";
            this.bodyList.Size = new System.Drawing.Size(660, 176);
            this.bodyList.TabIndex = 0;
            this.bodyList.UseCompatibleStateImageBehavior = false;
            this.bodyList.View = System.Windows.Forms.View.Details;
            this.bodyList.SelectedIndexChanged += new System.EventHandler(this.bodyList_SelectedIndexChanged);
            // 
            // columnBodyName
            // 
            this.columnBodyName.Text = "Body";
            this.columnBodyName.Width = 120;
            // 
            // columnBodyShape
            // 
            this.columnBodyShape.Text = "Shape";
            this.columnBodyShape.Width = 140;
            // 
            // columnBodyMotion
            // 
            this.columnBodyMotion.Text = "Motion";
            this.columnBodyMotion.Width = 70;
            // 
            // columnBodyMass
            // 
            this.columnBodyMass.Text = "Mass";
            this.columnBodyMass.Width = 60;
            // 
            // columnBodyFilter
            // 
            this.columnBodyFilter.Text = "Filter";
            this.columnBodyFilter.Width = 60;
            // 
            // columnBodyRadius
            // 
            this.columnBodyRadius.Text = "Radius";
            this.columnBodyRadius.Width = 60;
            // 
            // columnBodyDamping
            // 
            this.columnBodyDamping.Text = "LinDamp";
            this.columnBodyDamping.Width = 60;
            // 
            // previewStatus
            // 
            this.previewStatus.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.previewStatus.Location = new System.Drawing.Point(8, 591);
            this.previewStatus.Name = "previewStatus";
            this.previewStatus.Size = new System.Drawing.Size(660, 21);
            this.previewStatus.TabIndex = 1;
            this.previewStatus.Text = "No selection";
            this.previewStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // EditPhysicsSystem
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1100, 620);
            this.Controls.Add(this.splitMain);
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MinimizeBox = false;
            this.Name = "EditPhysicsSystem";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Select Physics System";
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel1.PerformLayout();
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.bottomPanel.ResumeLayout(false);
            this.bottomPanel.PerformLayout();
            this.splitPreview.Panel1.ResumeLayout(false);
            this.splitPreview.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitPreview)).EndInit();
            this.splitPreview.ResumeLayout(false);
            this.previewGroup.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.SplitContainer splitMain;
        private System.Windows.Forms.TextBox searchBox;
        private System.Windows.Forms.ListView systemList;
        private System.Windows.Forms.ColumnHeader columnIndex;
        private System.Windows.Forms.ColumnHeader columnLeaf;
        private System.Windows.Forms.ColumnHeader columnPath;
        private System.Windows.Forms.Panel bottomPanel;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.Button selectButton;
        private System.Windows.Forms.Label searchLabel;
        private System.Windows.Forms.GroupBox previewGroup;
        private System.Windows.Forms.SplitContainer splitPreview;
        private System.Windows.Forms.Integration.ElementHost modelRendererHost;
        private System.Windows.Forms.ListView bodyList;
        private System.Windows.Forms.ColumnHeader columnBodyName;
        private System.Windows.Forms.ColumnHeader columnBodyShape;
        private System.Windows.Forms.ColumnHeader columnBodyMotion;
        private System.Windows.Forms.ColumnHeader columnBodyMass;
        private System.Windows.Forms.ColumnHeader columnBodyFilter;
        private System.Windows.Forms.ColumnHeader columnBodyRadius;
        private System.Windows.Forms.ColumnHeader columnBodyDamping;
        private System.Windows.Forms.Label bodyDetailLabel;
        private System.Windows.Forms.Label previewStatus;
    }
}

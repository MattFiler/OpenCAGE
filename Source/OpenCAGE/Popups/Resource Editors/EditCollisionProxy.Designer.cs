namespace OpenCAGE
{
    partial class EditCollisionProxy
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
            this.compoundList = new System.Windows.Forms.ListView();
            this.columnProxy = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnInstances = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnOffset = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnRole = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.bottomPanel = new System.Windows.Forms.Panel();
            this.statusLabel = new System.Windows.Forms.Label();
            this.selectButton = new System.Windows.Forms.Button();
            this.searchLabel = new System.Windows.Forms.Label();
            this.previewGroup = new System.Windows.Forms.GroupBox();
            this.modelRendererHost = new System.Windows.Forms.Integration.ElementHost();
            this.previewStatus = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            this.bottomPanel.SuspendLayout();
            this.previewGroup.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitMain
            // 
            this.splitMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.Location = new System.Drawing.Point(0, 0);
            this.splitMain.Name = "splitMain";
            // 
            // splitMain.Panel1
            // 
            this.splitMain.Panel1.Controls.Add(this.compoundList);
            this.splitMain.Panel1.Controls.Add(this.searchBox);
            this.splitMain.Panel1.Controls.Add(this.searchLabel);
            this.splitMain.Panel1.Controls.Add(this.bottomPanel);
            this.splitMain.Panel1MinSize = 280;
            // 
            // splitMain.Panel2
            // 
            this.splitMain.Panel2.Controls.Add(this.previewGroup);
            this.splitMain.Panel2MinSize = 280;
            this.splitMain.Size = new System.Drawing.Size(980, 520);
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
            // compoundList
            // 
            this.compoundList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.compoundList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnProxy,
            this.columnInstances,
            this.columnOffset,
            this.columnRole});
            this.compoundList.FullRowSelect = true;
            this.compoundList.GridLines = true;
            this.compoundList.HideSelection = false;
            this.compoundList.Location = new System.Drawing.Point(12, 42);
            this.compoundList.MultiSelect = false;
            this.compoundList.Name = "compoundList";
            this.compoundList.Size = new System.Drawing.Size(393, 430);
            this.compoundList.TabIndex = 2;
            this.compoundList.UseCompatibleStateImageBehavior = false;
            this.compoundList.View = System.Windows.Forms.View.Details;
            this.compoundList.SelectedIndexChanged += new System.EventHandler(this.compoundList_SelectedIndexChanged);
            this.compoundList.DoubleClick += new System.EventHandler(this.compoundList_DoubleClick);
            // 
            // columnProxy
            // 
            this.columnProxy.Text = "Proxy";
            this.columnProxy.Width = 55;
            // 
            // columnInstances
            // 
            this.columnInstances.Text = "Instances";
            this.columnInstances.Width = 70;
            // 
            // columnOffset
            // 
            this.columnOffset.Text = "Data Offset";
            this.columnOffset.Width = 90;
            // 
            // columnRole
            // 
            this.columnRole.Text = "Role";
            this.columnRole.Width = 140;
            // 
            // bottomPanel
            // 
            this.bottomPanel.Controls.Add(this.statusLabel);
            this.bottomPanel.Controls.Add(this.selectButton);
            this.bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottomPanel.Location = new System.Drawing.Point(0, 482);
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
            this.selectButton.Text = "Select Compound";
            this.selectButton.UseVisualStyleBackColor = true;
            this.selectButton.Click += new System.EventHandler(this.selectButton_Click);
            // 
            // previewGroup
            // 
            this.previewGroup.Controls.Add(this.modelRendererHost);
            this.previewGroup.Controls.Add(this.previewStatus);
            this.previewGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.previewGroup.Location = new System.Drawing.Point(0, 0);
            this.previewGroup.Name = "previewGroup";
            this.previewGroup.Padding = new System.Windows.Forms.Padding(8);
            this.previewGroup.Size = new System.Drawing.Size(556, 520);
            this.previewGroup.TabIndex = 0;
            this.previewGroup.TabStop = false;
            this.previewGroup.Text = "Preview";
            // 
            // modelRendererHost
            // 
            this.modelRendererHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.modelRendererHost.Location = new System.Drawing.Point(8, 21);
            this.modelRendererHost.Name = "modelRendererHost";
            this.modelRendererHost.Size = new System.Drawing.Size(540, 470);
            this.modelRendererHost.TabIndex = 0;
            this.modelRendererHost.Child = null;
            // 
            // previewStatus
            // 
            this.previewStatus.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.previewStatus.Location = new System.Drawing.Point(8, 491);
            this.previewStatus.Name = "previewStatus";
            this.previewStatus.Size = new System.Drawing.Size(540, 21);
            this.previewStatus.TabIndex = 1;
            this.previewStatus.Text = "No selection";
            this.previewStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // EditCollisionProxy
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(980, 520);
            this.Controls.Add(this.splitMain);
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MinimizeBox = false;
            this.Name = "EditCollisionProxy";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Select Collision Havok Compound";
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel1.PerformLayout();
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.bottomPanel.ResumeLayout(false);
            this.bottomPanel.PerformLayout();
            this.previewGroup.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.SplitContainer splitMain;
        private System.Windows.Forms.TextBox searchBox;
        private System.Windows.Forms.ListView compoundList;
        private System.Windows.Forms.ColumnHeader columnProxy;
        private System.Windows.Forms.ColumnHeader columnInstances;
        private System.Windows.Forms.ColumnHeader columnOffset;
        private System.Windows.Forms.ColumnHeader columnRole;
        private System.Windows.Forms.Panel bottomPanel;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.Button selectButton;
        private System.Windows.Forms.Label searchLabel;
        private System.Windows.Forms.GroupBox previewGroup;
        private System.Windows.Forms.Integration.ElementHost modelRendererHost;
        private System.Windows.Forms.Label previewStatus;
    }
}

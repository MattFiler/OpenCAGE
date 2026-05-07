namespace OpenCAGE
{
    partial class EditMaterialMapping
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditMaterialMapping));
            this.materialMappingTreeView = new System.Windows.Forms.TreeView();
            this.mappingsListView = new System.Windows.Forms.ListView();
            this.columnFrom = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnTo = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.selectButton = new System.Windows.Forms.Button();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.treeViewButtonPanel = new System.Windows.Forms.Panel();
            this.addNewSetButton = new System.Windows.Forms.Button();
            this.buttonPanel = new System.Windows.Forms.Panel();
            this.selectFromMaterialButton = new System.Windows.Forms.Button();
            this.selectToMaterialButton = new System.Windows.Forms.Button();
            this.addMappingButton = new System.Windows.Forms.Button();
            this.removeMappingButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.treeViewButtonPanel.SuspendLayout();
            this.buttonPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // materialMappingTreeView
            // 
            this.materialMappingTreeView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.materialMappingTreeView.FullRowSelect = true;
            this.materialMappingTreeView.HideSelection = false;
            this.materialMappingTreeView.Location = new System.Drawing.Point(0, 0);
            this.materialMappingTreeView.Name = "materialMappingTreeView";
            this.materialMappingTreeView.Size = new System.Drawing.Size(277, 420);
            this.materialMappingTreeView.TabIndex = 0;
            this.materialMappingTreeView.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.materialMappingTreeView_AfterSelect);
            // 
            // mappingsListView
            // 
            this.mappingsListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnFrom,
            this.columnTo});
            this.mappingsListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mappingsListView.FullRowSelect = true;
            this.mappingsListView.GridLines = true;
            this.mappingsListView.HideSelection = false;
            this.mappingsListView.Location = new System.Drawing.Point(0, 0);
            this.mappingsListView.Name = "mappingsListView";
            this.mappingsListView.Size = new System.Drawing.Size(704, 420);
            this.mappingsListView.TabIndex = 0;
            this.mappingsListView.UseCompatibleStateImageBehavior = false;
            this.mappingsListView.View = System.Windows.Forms.View.Details;
            this.mappingsListView.DoubleClick += new System.EventHandler(this.mappingsListView_DoubleClick);
            this.mappingsListView.KeyDown += new System.Windows.Forms.KeyEventHandler(this.mappingsListView_KeyDown);
            // 
            // columnFrom
            // 
            this.columnFrom.Text = "From";
            this.columnFrom.Width = 340;
            // 
            // columnTo
            // 
            this.columnTo.Text = "To";
            this.columnTo.Width = 340;
            // 
            // selectButton
            // 
            this.selectButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.selectButton.Location = new System.Drawing.Point(177, 3);
            this.selectButton.Name = "selectButton";
            this.selectButton.Size = new System.Drawing.Size(100, 24);
            this.selectButton.TabIndex = 1;
            this.selectButton.Text = "Select Set";
            this.selectButton.UseVisualStyleBackColor = true;
            this.selectButton.Click += new System.EventHandler(this.selectButton_Click);
            // 
            // splitContainer
            // 
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.Location = new System.Drawing.Point(0, 0);
            this.splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.materialMappingTreeView);
            this.splitContainer.Panel1.Controls.Add(this.treeViewButtonPanel);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.mappingsListView);
            this.splitContainer.Panel2.Controls.Add(this.buttonPanel);
            this.splitContainer.Size = new System.Drawing.Size(985, 450);
            this.splitContainer.SplitterDistance = 277;
            this.splitContainer.TabIndex = 0;
            // 
            // treeViewButtonPanel
            // 
            this.treeViewButtonPanel.Controls.Add(this.addNewSetButton);
            this.treeViewButtonPanel.Controls.Add(this.selectButton);
            this.treeViewButtonPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.treeViewButtonPanel.Location = new System.Drawing.Point(0, 420);
            this.treeViewButtonPanel.Name = "treeViewButtonPanel";
            this.treeViewButtonPanel.Size = new System.Drawing.Size(277, 30);
            this.treeViewButtonPanel.TabIndex = 1;
            // 
            // addNewSetButton
            // 
            this.addNewSetButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.addNewSetButton.Location = new System.Drawing.Point(3, 3);
            this.addNewSetButton.Name = "addNewSetButton";
            this.addNewSetButton.Size = new System.Drawing.Size(100, 24);
            this.addNewSetButton.TabIndex = 0;
            this.addNewSetButton.Text = "Add New Set";
            this.addNewSetButton.UseVisualStyleBackColor = true;
            this.addNewSetButton.Click += new System.EventHandler(this.addNewSetButton_Click);
            // 
            // buttonPanel
            // 
            this.buttonPanel.Controls.Add(this.selectFromMaterialButton);
            this.buttonPanel.Controls.Add(this.selectToMaterialButton);
            this.buttonPanel.Controls.Add(this.addMappingButton);
            this.buttonPanel.Controls.Add(this.removeMappingButton);
            this.buttonPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.buttonPanel.Location = new System.Drawing.Point(0, 420);
            this.buttonPanel.Name = "buttonPanel";
            this.buttonPanel.Size = new System.Drawing.Size(704, 30);
            this.buttonPanel.TabIndex = 2;
            // 
            // selectFromMaterialButton
            // 
            this.selectFromMaterialButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.selectFromMaterialButton.Location = new System.Drawing.Point(3, 3);
            this.selectFromMaterialButton.Name = "selectFromMaterialButton";
            this.selectFromMaterialButton.Size = new System.Drawing.Size(100, 24);
            this.selectFromMaterialButton.TabIndex = 2;
            this.selectFromMaterialButton.Text = "Edit From";
            this.selectFromMaterialButton.UseVisualStyleBackColor = true;
            this.selectFromMaterialButton.Click += new System.EventHandler(this.selectFromMaterialButton_Click);
            // 
            // selectToMaterialButton
            // 
            this.selectToMaterialButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.selectToMaterialButton.Location = new System.Drawing.Point(109, 3);
            this.selectToMaterialButton.Name = "selectToMaterialButton";
            this.selectToMaterialButton.Size = new System.Drawing.Size(100, 24);
            this.selectToMaterialButton.TabIndex = 3;
            this.selectToMaterialButton.Text = "Edit To";
            this.selectToMaterialButton.UseVisualStyleBackColor = true;
            this.selectToMaterialButton.Click += new System.EventHandler(this.selectToMaterialButton_Click);
            // 
            // addMappingButton
            // 
            this.addMappingButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.addMappingButton.Location = new System.Drawing.Point(495, 3);
            this.addMappingButton.Name = "addMappingButton";
            this.addMappingButton.Size = new System.Drawing.Size(100, 24);
            this.addMappingButton.TabIndex = 0;
            this.addMappingButton.Text = "Add Mapping";
            this.addMappingButton.UseVisualStyleBackColor = true;
            this.addMappingButton.Click += new System.EventHandler(this.addMappingButton_Click);
            // 
            // removeMappingButton
            // 
            this.removeMappingButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.removeMappingButton.Location = new System.Drawing.Point(601, 3);
            this.removeMappingButton.Name = "removeMappingButton";
            this.removeMappingButton.Size = new System.Drawing.Size(100, 24);
            this.removeMappingButton.TabIndex = 1;
            this.removeMappingButton.Text = "Remove Mapping";
            this.removeMappingButton.UseVisualStyleBackColor = true;
            this.removeMappingButton.Click += new System.EventHandler(this.removeMappingButton_Click);
            // 
            // EditMaterialMapping
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(985, 450);
            this.Controls.Add(this.splitContainer);
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.Name = "EditMaterialMapping";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Edit Material Mapping";
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.treeViewButtonPanel.ResumeLayout(false);
            this.buttonPanel.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.TreeView materialMappingTreeView;
        private System.Windows.Forms.ListView mappingsListView;
        private System.Windows.Forms.ColumnHeader columnFrom;
        private System.Windows.Forms.ColumnHeader columnTo;
        private System.Windows.Forms.Button selectButton;
        private System.Windows.Forms.Panel buttonPanel;
        private System.Windows.Forms.Button addMappingButton;
        private System.Windows.Forms.Button removeMappingButton;
        private System.Windows.Forms.Button addNewSetButton;
        private System.Windows.Forms.Panel treeViewButtonPanel;
        private System.Windows.Forms.Button selectFromMaterialButton;
        private System.Windows.Forms.Button selectToMaterialButton;
    }
}



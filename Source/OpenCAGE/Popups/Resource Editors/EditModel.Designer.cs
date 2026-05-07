namespace OpenCAGE
{
    partial class EditModel
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditModel));
            this.modelRendererHost = new System.Windows.Forms.Integration.ElementHost();
            this.editGeometryBtn = new System.Windows.Forms.Button();
            this.exportCs2Btn = new System.Windows.Forms.Button();
            this.importModelBtn = new System.Windows.Forms.Button();
            this.FileTree = new System.Windows.Forms.TreeView();
            this.modelPreviewArea = new System.Windows.Forms.GroupBox();
            this.useMaterials = new System.Windows.Forms.CheckBox();
            this.selectModelBtn = new System.Windows.Forms.Button();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.deleteBtn = new System.Windows.Forms.Button();
            this.renderFlagsGroup = new System.Windows.Forms.GroupBox();
            this.renderFlagsPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.submeshFilterGroup = new System.Windows.Forms.GroupBox();
            this.submeshFilterPanel = new System.Windows.Forms.Panel();
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            this.modelPreviewArea.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.renderFlagsGroup.SuspendLayout();
            this.submeshFilterGroup.SuspendLayout();
            this.SuspendLayout();
            // 
            // modelRendererHost
            // 
            this.modelRendererHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.modelRendererHost.Location = new System.Drawing.Point(3, 16);
            this.modelRendererHost.Name = "modelRendererHost";
            this.modelRendererHost.Size = new System.Drawing.Size(473, 610);
            this.modelRendererHost.TabIndex = 0;
            this.modelRendererHost.Text = "elementHost1";
            this.modelRendererHost.Child = null;
            // 
            // editGeometryBtn
            // 
            this.editGeometryBtn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editGeometryBtn.Location = new System.Drawing.Point(3, 3);
            this.editGeometryBtn.Name = "editGeometryBtn";
            this.editGeometryBtn.Size = new System.Drawing.Size(196, 23);
            this.editGeometryBtn.TabIndex = 2;
            this.editGeometryBtn.Text = "Edit";
            this.editGeometryBtn.UseVisualStyleBackColor = true;
            this.editGeometryBtn.Click += new System.EventHandler(this.editGeometryBtn_Click);
            // 
            // exportCs2Btn
            // 
            this.exportCs2Btn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.exportCs2Btn.Location = new System.Drawing.Point(3, 32);
            this.exportCs2Btn.Name = "exportCs2Btn";
            this.exportCs2Btn.Size = new System.Drawing.Size(196, 23);
            this.exportCs2Btn.TabIndex = 1;
            this.exportCs2Btn.Text = "Export";
            this.exportCs2Btn.UseVisualStyleBackColor = true;
            this.exportCs2Btn.Click += new System.EventHandler(this.exportCs2Btn_Click);
            // 
            // importModelBtn
            // 
            this.importModelBtn.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.importModelBtn.Location = new System.Drawing.Point(0, 602);
            this.importModelBtn.Name = "importModelBtn";
            this.importModelBtn.Size = new System.Drawing.Size(352, 29);
            this.importModelBtn.TabIndex = 0;
            this.importModelBtn.Text = "Import New";
            this.importModelBtn.UseVisualStyleBackColor = true;
            this.importModelBtn.Click += new System.EventHandler(this.importModelBtn_Click);
            // 
            // FileTree
            // 
            this.FileTree.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.FileTree.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.FileTree.FullRowSelect = true;
            this.FileTree.HideSelection = false;
            this.FileTree.Location = new System.Drawing.Point(0, 0);
            this.FileTree.Name = "FileTree";
            this.FileTree.Size = new System.Drawing.Size(352, 597);
            this.FileTree.TabIndex = 100;
            this.FileTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.FileTree_AfterSelect);
            // 
            // modelPreviewArea
            // 
            this.modelPreviewArea.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.modelPreviewArea.Controls.Add(this.useMaterials);
            this.modelPreviewArea.Controls.Add(this.selectModelBtn);
            this.modelPreviewArea.Controls.Add(this.modelRendererHost);
            this.modelPreviewArea.Location = new System.Drawing.Point(3, 3);
            this.modelPreviewArea.Name = "modelPreviewArea";
            this.modelPreviewArea.Size = new System.Drawing.Size(479, 629);
            this.modelPreviewArea.TabIndex = 103;
            this.modelPreviewArea.TabStop = false;
            // 
            // useMaterials
            // 
            this.useMaterials.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.useMaterials.AutoSize = true;
            this.useMaterials.Location = new System.Drawing.Point(6, 606);
            this.useMaterials.Name = "useMaterials";
            this.useMaterials.Size = new System.Drawing.Size(111, 17);
            this.useMaterials.TabIndex = 2;
            this.useMaterials.Text = "Use Material Data";
            this.useMaterials.UseVisualStyleBackColor = true;
            this.useMaterials.CheckedChanged += new System.EventHandler(this.useMaterials_CheckedChanged);
            // 
            // selectModelBtn
            // 
            this.selectModelBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.selectModelBtn.Enabled = false;
            this.selectModelBtn.Location = new System.Drawing.Point(330, 579);
            this.selectModelBtn.Name = "selectModelBtn";
            this.selectModelBtn.Size = new System.Drawing.Size(134, 35);
            this.selectModelBtn.TabIndex = 1;
            this.selectModelBtn.Text = "Select This Model";
            this.selectModelBtn.UseVisualStyleBackColor = true;
            this.selectModelBtn.Click += new System.EventHandler(this.selectModel_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(7, 8);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.importModelBtn);
            this.splitContainer1.Panel1.Controls.Add(this.FileTree);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.splitContainer2);
            this.splitContainer1.Size = new System.Drawing.Size(1057, 632);
            this.splitContainer1.SplitterDistance = 352;
            this.splitContainer1.TabIndex = 104;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainer2.IsSplitterFixed = true;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.modelPreviewArea);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.tableLayoutPanel2);
            this.splitContainer2.Panel2.Controls.Add(this.renderFlagsGroup);
            this.splitContainer2.Panel2.Controls.Add(this.submeshFilterGroup);
            this.splitContainer2.Size = new System.Drawing.Size(701, 632);
            this.splitContainer2.SplitterDistance = 485;
            this.splitContainer2.SplitterWidth = 5;
            this.splitContainer2.TabIndex = 0;
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel2.ColumnCount = 1;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.Controls.Add(this.deleteBtn, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this.exportCs2Btn, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this.editGeometryBtn, 0, 0);
            this.tableLayoutPanel2.Location = new System.Drawing.Point(0, 544);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 3;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(202, 89);
            this.tableLayoutPanel2.TabIndex = 190;
            // 
            // deleteBtn
            // 
            this.deleteBtn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.deleteBtn.Location = new System.Drawing.Point(3, 61);
            this.deleteBtn.Name = "deleteBtn";
            this.deleteBtn.Size = new System.Drawing.Size(196, 25);
            this.deleteBtn.TabIndex = 3;
            this.deleteBtn.Text = "Delete";
            this.deleteBtn.UseVisualStyleBackColor = true;
            this.deleteBtn.Click += new System.EventHandler(this.deleteBtn_Click);
            // 
            // renderFlagsGroup
            // 
            this.renderFlagsGroup.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.renderFlagsGroup.Controls.Add(this.renderFlagsPanel);
            this.renderFlagsGroup.Location = new System.Drawing.Point(0, 346);
            this.renderFlagsGroup.Name = "renderFlagsGroup";
            this.renderFlagsGroup.Size = new System.Drawing.Size(201, 192);
            this.renderFlagsGroup.TabIndex = 191;
            this.renderFlagsGroup.TabStop = false;
            this.renderFlagsGroup.Text = "Model Render Flags";
            // 
            // renderFlagsPanel
            // 
            this.renderFlagsPanel.AutoScroll = true;
            this.renderFlagsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.renderFlagsPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.renderFlagsPanel.Location = new System.Drawing.Point(3, 16);
            this.renderFlagsPanel.Margin = new System.Windows.Forms.Padding(0);
            this.renderFlagsPanel.Name = "renderFlagsPanel";
            this.renderFlagsPanel.Padding = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.renderFlagsPanel.Size = new System.Drawing.Size(195, 173);
            this.renderFlagsPanel.TabIndex = 0;
            this.renderFlagsPanel.WrapContents = false;
            // 
            // submeshFilterGroup
            // 
            this.submeshFilterGroup.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.submeshFilterGroup.Controls.Add(this.submeshFilterPanel);
            this.submeshFilterGroup.Location = new System.Drawing.Point(0, 0);
            this.submeshFilterGroup.Name = "submeshFilterGroup";
            this.submeshFilterGroup.Size = new System.Drawing.Size(201, 340);
            this.submeshFilterGroup.TabIndex = 0;
            this.submeshFilterGroup.TabStop = false;
            this.submeshFilterGroup.Text = "Preview Render Filter";
            // 
            // submeshFilterPanel
            // 
            this.submeshFilterPanel.AutoScroll = true;
            this.submeshFilterPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.submeshFilterPanel.Location = new System.Drawing.Point(3, 16);
            this.submeshFilterPanel.Name = "submeshFilterPanel";
            this.submeshFilterPanel.Size = new System.Drawing.Size(195, 321);
            this.submeshFilterPanel.TabIndex = 0;
            // 
            // imageList1
            // 
            this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
            this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList1.Images.SetKeyName(0, "Folder Icon.png");
            this.imageList1.Images.SetKeyName(1, "file_icon.png");
            this.imageList1.Images.SetKeyName(2, "FolderOpened Icon.png");
            // 
            // EditModel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1069, 647);
            this.Controls.Add(this.splitContainer1);
            this.DoubleBuffered = true;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.Name = "EditModel";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Model Editor";
            this.modelPreviewArea.ResumeLayout(false);
            this.modelPreviewArea.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.renderFlagsGroup.ResumeLayout(false);
            this.submeshFilterGroup.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Integration.ElementHost modelRendererHost;
        private System.Windows.Forms.Button importModelBtn;
        private System.Windows.Forms.Button exportCs2Btn;
        private System.Windows.Forms.Button editGeometryBtn;
        private System.Windows.Forms.TreeView FileTree;
        private System.Windows.Forms.GroupBox modelPreviewArea;
        private System.Windows.Forms.Button selectModelBtn;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.CheckBox useMaterials;
        private System.Windows.Forms.GroupBox submeshFilterGroup;
        private System.Windows.Forms.Panel submeshFilterPanel;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.Button deleteBtn;
        private System.Windows.Forms.ImageList imageList1;
        private System.Windows.Forms.GroupBox renderFlagsGroup;
        private System.Windows.Forms.FlowLayoutPanel renderFlagsPanel;
    }
}

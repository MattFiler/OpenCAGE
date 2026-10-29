namespace OpenCAGE
{
    partial class SelectComposite
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SelectComposite));
            this.selectComp = new System.Windows.Forms.Button();
            this.FileTree = new System.Windows.Forms.TreeView();
            this.searchBox = new System.Windows.Forms.TextBox();
            this.searchButton = new System.Windows.Forms.Button();
            this.clearSearchBtn = new System.Windows.Forms.Button();
            this.previewSplit = new System.Windows.Forms.SplitContainer();
            this.compositePreview = new OpenCAGE.Popups.UserControls.CompositePreviewPane();
            this.showPreview = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.previewSplit)).BeginInit();
            this.previewSplit.Panel1.SuspendLayout();
            this.previewSplit.Panel2.SuspendLayout();
            this.previewSplit.SuspendLayout();
            this.SuspendLayout();
            // 
            // selectComp
            // 
            this.selectComp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.selectComp.Location = new System.Drawing.Point(577, 633);
            this.selectComp.Name = "selectComp";
            this.selectComp.Size = new System.Drawing.Size(171, 23);
            this.selectComp.TabIndex = 2;
            this.selectComp.Text = "Select Composite";
            this.selectComp.UseVisualStyleBackColor = true;
            this.selectComp.Click += new System.EventHandler(this.SelectEntity_Click);
            // 
            // FileTree
            // 
            this.FileTree.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.FileTree.FullRowSelect = true;
            this.FileTree.HideSelection = false;
            this.FileTree.ImageIndex = 0;
            this.FileTree.Location = new System.Drawing.Point(0, 24);
            this.FileTree.Name = "FileTree";
            this.FileTree.SelectedImageIndex = 0;
            this.FileTree.Size = new System.Drawing.Size(450, 587);
            this.FileTree.TabIndex = 3;
            this.FileTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.FileTree_AfterSelect);
            // 
            // searchBox
            // 
            this.searchBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.searchBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.searchBox.Location = new System.Drawing.Point(0, 1);
            this.searchBox.Name = "searchBox";
            this.searchBox.Size = new System.Drawing.Size(353, 20);
            this.searchBox.TabIndex = 0;
            this.searchBox.TextChanged += new System.EventHandler(this.searchBox_TextChanged);
            this.searchBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.searchBox_KeyDown);
            // 
            // searchButton
            // 
            this.searchButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.searchButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.searchButton.Location = new System.Drawing.Point(355, 0);
            this.searchButton.Name = "searchButton";
            this.searchButton.Size = new System.Drawing.Size(70, 23);
            this.searchButton.TabIndex = 1;
            this.searchButton.Text = "Search";
            this.searchButton.UseVisualStyleBackColor = true;
            this.searchButton.Click += new System.EventHandler(this.searchButton_Click);
            // 
            // clearSearchBtn
            // 
            this.clearSearchBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.clearSearchBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.clearSearchBtn.Image = ((System.Drawing.Image)(resources.GetObject("clearSearchBtn.Image")));
            this.clearSearchBtn.Location = new System.Drawing.Point(427, 0);
            this.clearSearchBtn.Name = "clearSearchBtn";
            this.clearSearchBtn.Padding = new System.Windows.Forms.Padding(0, 0, 0, 2);
            this.clearSearchBtn.Size = new System.Drawing.Size(23, 23);
            this.clearSearchBtn.TabIndex = 2;
            this.clearSearchBtn.UseVisualStyleBackColor = true;
            this.clearSearchBtn.Click += new System.EventHandler(this.clearSearchBtn_Click);
            // 
            // previewSplit
            // 
            this.previewSplit.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.previewSplit.Location = new System.Drawing.Point(12, 12);
            this.previewSplit.Name = "previewSplit";
            // 
            // previewSplit.Panel1
            // 
            this.previewSplit.Panel1.Controls.Add(this.searchBox);
            this.previewSplit.Panel1.Controls.Add(this.searchButton);
            this.previewSplit.Panel1.Controls.Add(this.clearSearchBtn);
            this.previewSplit.Panel1.Controls.Add(this.FileTree);
            // 
            // previewSplit.Panel2
            // 
            this.previewSplit.Panel2.Controls.Add(this.compositePreview);
            this.previewSplit.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.previewSplit.Size = new System.Drawing.Size(736, 611);
            this.previewSplit.SplitterWidth = 6;
            this.previewSplit.SplitterDistance = 450;
            this.previewSplit.Panel1MinSize = 200;
            this.previewSplit.Panel2MinSize = 146;
            this.previewSplit.TabIndex = 0;
            this.previewSplit.TabStop = false;
            // 
            // compositePreview
            // 
            this.compositePreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.compositePreview.Location = new System.Drawing.Point(0, 0);
            this.compositePreview.Name = "compositePreview";
            this.compositePreview.Size = new System.Drawing.Size(280, 611);
            this.compositePreview.TabIndex = 0;
            // 
            // showPreview
            // 
            this.showPreview.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.showPreview.AutoSize = true;
            this.showPreview.Checked = true;
            this.showPreview.CheckState = System.Windows.Forms.CheckState.Checked;
            this.showPreview.Location = new System.Drawing.Point(12, 636);
            this.showPreview.Name = "showPreview";
            this.showPreview.Size = new System.Drawing.Size(93, 17);
            this.showPreview.TabIndex = 1;
            this.showPreview.Text = "Show Preview";
            this.showPreview.UseVisualStyleBackColor = true;
            this.showPreview.CheckedChanged += new System.EventHandler(this.showPreview_CheckedChanged);
            // 
            // SelectComposite
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(760, 664);
            this.Controls.Add(this.previewSplit);
            this.Controls.Add(this.showPreview);
            this.Controls.Add(this.selectComp);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.MinimumSize = new System.Drawing.Size(528, 300);
            this.Name = "SelectComposite";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Select Composite";
            this.previewSplit.Panel1.ResumeLayout(false);
            this.previewSplit.Panel1.PerformLayout();
            this.previewSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.previewSplit)).EndInit();
            this.previewSplit.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button selectComp;
        private System.Windows.Forms.TreeView FileTree;
        private System.Windows.Forms.TextBox searchBox;
        private System.Windows.Forms.Button searchButton;
        private System.Windows.Forms.Button clearSearchBtn;
        private System.Windows.Forms.SplitContainer previewSplit;
        private OpenCAGE.Popups.UserControls.CompositePreviewPane compositePreview;
        private System.Windows.Forms.CheckBox showPreview;
    }
}

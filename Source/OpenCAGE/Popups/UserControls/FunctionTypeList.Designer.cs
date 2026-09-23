namespace OpenCAGE.Popups.UserControls
{
    partial class FunctionTypeList
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FunctionTypeList));
            this.functionTree = new System.Windows.Forms.TreeView();
            this.entityListIcons = new System.Windows.Forms.ImageList(this.components);
            this.clearSearchBtn = new System.Windows.Forms.Button();
            this.searchText = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // functionTree
            // 
            this.functionTree.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.functionTree.HideSelection = false;
            this.functionTree.ImageIndex = 0;
            this.functionTree.ImageList = this.entityListIcons;
            this.functionTree.Location = new System.Drawing.Point(2, 26);
            this.functionTree.Name = "functionTree";
            this.functionTree.SelectedImageIndex = 0;
            this.functionTree.ShowNodeToolTips = true;
            this.functionTree.Size = new System.Drawing.Size(626, 252);
            this.functionTree.TabIndex = 185;
            this.functionTree.AfterCollapse += new System.Windows.Forms.TreeViewEventHandler(this.functionTree_AfterCollapse);
            this.functionTree.AfterExpand += new System.Windows.Forms.TreeViewEventHandler(this.functionTree_AfterExpand);
            this.functionTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.functionTree_AfterSelect);
            // 
            // entityListIcons
            // 
            this.entityListIcons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("entityListIcons.ImageStream")));
            this.entityListIcons.TransparentColor = System.Drawing.Color.Transparent;
            this.entityListIcons.Images.SetKeyName(0, "AnimatorController Icon.png");
            this.entityListIcons.Images.SetKeyName(1, "d_ScriptableObject Icon braces only.png");
            this.entityListIcons.Images.SetKeyName(2, "d_PrefabVariant Icon.png");
            this.entityListIcons.Images.SetKeyName(3, "d_ScriptableObject Icon.png");
            this.entityListIcons.Images.SetKeyName(4, "AreaEffector2D Icon.png");
            this.entityListIcons.Images.SetKeyName(5, "pin_bottom_out.png");
            this.entityListIcons.Images.SetKeyName(6, "pin_left_in.png");
            this.entityListIcons.Images.SetKeyName(7, "pin_right_out.png");
            this.entityListIcons.Images.SetKeyName(8, "pin_top_in.png");
            this.entityListIcons.Images.SetKeyName(9, "pin_top_out.png");
            this.entityListIcons.Images.SetKeyName(10, "Folder_6222.ico");
            this.entityListIcons.Images.SetKeyName(11, "Folder_6221.ico");
            // 
            // clearSearchBtn
            // 
            this.clearSearchBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.clearSearchBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.clearSearchBtn.Image = ((System.Drawing.Image)(resources.GetObject("clearSearchBtn.Image")));
            this.clearSearchBtn.Location = new System.Drawing.Point(608, 2);
            this.clearSearchBtn.Name = "clearSearchBtn";
            this.clearSearchBtn.Size = new System.Drawing.Size(20, 20);
            this.clearSearchBtn.TabIndex = 182;
            this.clearSearchBtn.UseVisualStyleBackColor = true;
            this.clearSearchBtn.Click += new System.EventHandler(this.clearSearchBtn_Click);
            // 
            // searchText
            // 
            this.searchText.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.searchText.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.searchText.Location = new System.Drawing.Point(2, 2);
            this.searchText.Name = "searchText";
            this.searchText.Size = new System.Drawing.Size(607, 20);
            this.searchText.TabIndex = 0;
            this.searchText.TextChanged += new System.EventHandler(this.searchText_TextChanged);
            // 
            // FunctionTypeList
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.functionTree);
            this.Controls.Add(this.clearSearchBtn);
            this.Controls.Add(this.searchText);
            this.Name = "FunctionTypeList";
            this.Size = new System.Drawing.Size(630, 280);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TreeView functionTree;
        private System.Windows.Forms.Button clearSearchBtn;
        private System.Windows.Forms.TextBox searchText;
        private System.Windows.Forms.ImageList entityListIcons;
    }
}

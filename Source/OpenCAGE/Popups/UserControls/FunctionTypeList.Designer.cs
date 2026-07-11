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
            this.functionTypes = new System.Windows.Forms.ListView();
            this.funcHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.inheritHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.clearSearchBtn = new System.Windows.Forms.Button();
            this.searchText = new System.Windows.Forms.TextBox();
            this.entityListIcons = new System.Windows.Forms.ImageList(this.components);
            this.SuspendLayout();
            // 
            // functionTypes
            // 
            this.functionTypes.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.functionTypes.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.funcHeader,
            this.inheritHeader});
            this.functionTypes.FullRowSelect = true;
            this.functionTypes.HideSelection = false;
            this.functionTypes.LargeImageList = this.entityListIcons;
            this.functionTypes.Location = new System.Drawing.Point(2, 26);
            this.functionTypes.MultiSelect = false;
            this.functionTypes.Name = "functionTypes";
            this.functionTypes.Size = new System.Drawing.Size(626, 252);
            this.functionTypes.SmallImageList = this.entityListIcons;
            this.functionTypes.TabIndex = 185;
            this.functionTypes.UseCompatibleStateImageBehavior = false;
            this.functionTypes.View = System.Windows.Forms.View.Details;
            this.functionTypes.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.functionTypes_ColumnClick);
            this.functionTypes.SelectedIndexChanged += new System.EventHandler(this.functionTypes_SelectedIndexChanged);
            // 
            // funcHeader
            // 
            this.funcHeader.Text = "Function";
            this.funcHeader.Width = 364;
            // 
            // inheritHeader
            // 
            this.inheritHeader.Text = "Inherits From";
            this.inheritHeader.Width = 232;
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
            // entityListIcons
            // 
            this.entityListIcons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("entityListIcons.ImageStream")));
            this.entityListIcons.TransparentColor = System.Drawing.Color.Transparent;
            this.entityListIcons.Images.SetKeyName(0, "d_ScriptableObject Icon braces only.png");
            // 
            // FunctionTypeList
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.functionTypes);
            this.Controls.Add(this.clearSearchBtn);
            this.Controls.Add(this.searchText);
            this.Name = "FunctionTypeList";
            this.Size = new System.Drawing.Size(630, 280);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListView functionTypes;
        private System.Windows.Forms.ColumnHeader funcHeader;
        private System.Windows.Forms.ColumnHeader inheritHeader;
        private System.Windows.Forms.Button clearSearchBtn;
        private System.Windows.Forms.TextBox searchText;
        private System.Windows.Forms.ImageList entityListIcons;
    }
}

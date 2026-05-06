namespace CommandsEditor.Popups.UserControls
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
            this.typesCount = new System.Windows.Forms.Label();
            this.clearSearchBtn = new System.Windows.Forms.Button();
            this.searchText = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
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
            this.functionTypes.Location = new System.Drawing.Point(2, 41);
            this.functionTypes.MultiSelect = false;
            this.functionTypes.Name = "functionTypes";
            this.functionTypes.Size = new System.Drawing.Size(626, 237);
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
            // typesCount
            // 
            this.typesCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.typesCount.AutoSize = true;
            this.typesCount.Location = new System.Drawing.Point(524, 6);
            this.typesCount.Name = "typesCount";
            this.typesCount.Size = new System.Drawing.Size(89, 13);
            this.typesCount.TabIndex = 184;
            this.typesCount.Text = "Showing 0 Types";
            this.typesCount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // clearSearchBtn
            // 
            this.clearSearchBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.clearSearchBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.clearSearchBtn.Image = ((System.Drawing.Image)(resources.GetObject("clearSearchBtn.Image")));
            this.clearSearchBtn.Location = new System.Drawing.Point(608, 22);
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
            this.searchText.Location = new System.Drawing.Point(2, 22);
            this.searchText.Name = "searchText";
            this.searchText.Size = new System.Drawing.Size(607, 20);
            this.searchText.TabIndex = 0;
            this.searchText.TextChanged += new System.EventHandler(this.searchText_TextChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(-1, 1);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(114, 18);
            this.label2.TabIndex = 183;
            this.label2.Text = "Function Type";
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
            this.Controls.Add(this.typesCount);
            this.Controls.Add(this.clearSearchBtn);
            this.Controls.Add(this.searchText);
            this.Controls.Add(this.label2);
            this.Name = "FunctionTypeList";
            this.Size = new System.Drawing.Size(630, 280);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListView functionTypes;
        private System.Windows.Forms.ColumnHeader funcHeader;
        private System.Windows.Forms.ColumnHeader inheritHeader;
        private System.Windows.Forms.Label typesCount;
        private System.Windows.Forms.Button clearSearchBtn;
        private System.Windows.Forms.TextBox searchText;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ImageList entityListIcons;
    }
}

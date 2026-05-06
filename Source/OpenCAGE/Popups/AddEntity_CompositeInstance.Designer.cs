namespace CommandsEditor
{
    partial class AddEntity_CompositeInstance
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddEntity_CompositeInstance));
            this.createEntity = new System.Windows.Forms.Button();
            this.entityName = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.searchText = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.clearSearchBtn = new System.Windows.Forms.Button();
            this.compositeTree = new System.Windows.Forms.TreeView();
            this.imageList = new System.Windows.Forms.ImageList(this.components);
            this.compositeNameDisplay = new System.Windows.Forms.TextBox();
            this.addDefaultParams = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // createEntity
            // 
            this.createEntity.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.createEntity.Location = new System.Drawing.Point(540, 366);
            this.createEntity.Name = "createEntity";
            this.createEntity.Size = new System.Drawing.Size(101, 23);
            this.createEntity.TabIndex = 6;
            this.createEntity.Text = "Create";
            this.createEntity.UseVisualStyleBackColor = true;
            this.createEntity.Click += new System.EventHandler(this.createEntity_Click);
            // 
            // entityName
            // 
            this.entityName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.entityName.Location = new System.Drawing.Point(15, 34);
            this.entityName.Name = "entityName";
            this.entityName.Size = new System.Drawing.Size(626, 20);
            this.entityName.TabIndex = 1;
            this.entityName.KeyDown += new System.Windows.Forms.KeyEventHandler(this.CreateEntityOnEnterKey);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(12, 67);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(178, 18);
            this.label2.TabIndex = 12;
            this.label2.Text = "Composite to Instance";
            // 
            // searchText
            // 
            this.searchText.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.searchText.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.searchText.Location = new System.Drawing.Point(15, 88);
            this.searchText.Name = "searchText";
            this.searchText.Size = new System.Drawing.Size(607, 20);
            this.searchText.TabIndex = 2;
            this.searchText.TextChanged += new System.EventHandler(this.searchText_TextChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(12, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(99, 18);
            this.label1.TabIndex = 147;
            this.label1.Text = "Entity Name";
            // 
            // clearSearchBtn
            // 
            this.clearSearchBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.clearSearchBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.clearSearchBtn.Image = ((System.Drawing.Image)(resources.GetObject("clearSearchBtn.Image")));
            this.clearSearchBtn.Location = new System.Drawing.Point(621, 88);
            this.clearSearchBtn.Name = "clearSearchBtn";
            this.clearSearchBtn.Size = new System.Drawing.Size(20, 20);
            this.clearSearchBtn.TabIndex = 3;
            this.clearSearchBtn.UseVisualStyleBackColor = true;
            this.clearSearchBtn.Click += new System.EventHandler(this.clearSearchBtn_Click);
            // 
            // compositeTree
            // 
            this.compositeTree.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.compositeTree.FullRowSelect = true;
            this.compositeTree.HideSelection = false;
            this.compositeTree.ImageIndex = 0;
            this.compositeTree.ImageList = this.imageList;
            this.compositeTree.Location = new System.Drawing.Point(15, 107);
            this.compositeTree.Name = "compositeTree";
            this.compositeTree.SelectedImageIndex = 0;
            this.compositeTree.Size = new System.Drawing.Size(626, 225);
            this.compositeTree.TabIndex = 5;
            this.compositeTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.compositeTree_AfterSelect);
            // 
            // imageList
            // 
            this.imageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList.ImageStream")));
            this.imageList.TransparentColor = System.Drawing.Color.Magenta;
            this.imageList.Images.SetKeyName(0, "Folder Icon.png");
            this.imageList.Images.SetKeyName(1, "d_Prefab Icon.png");
            this.imageList.Images.SetKeyName(2, "FolderOpened Icon.png");
            this.imageList.Images.SetKeyName(3, "globe.png");
            this.imageList.Images.SetKeyName(4, "cog.png");
            this.imageList.Images.SetKeyName(5, "Avatar Icon.png");
            // 
            // compositeNameDisplay
            // 
            this.compositeNameDisplay.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.compositeNameDisplay.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.compositeNameDisplay.Location = new System.Drawing.Point(15, 331);
            this.compositeNameDisplay.Name = "compositeNameDisplay";
            this.compositeNameDisplay.ReadOnly = true;
            this.compositeNameDisplay.Size = new System.Drawing.Size(626, 20);
            this.compositeNameDisplay.TabIndex = 148;
            // 
            // addDefaultParams
            // 
            this.addDefaultParams.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.addDefaultParams.AutoSize = true;
            this.addDefaultParams.Location = new System.Drawing.Point(15, 370);
            this.addDefaultParams.Name = "addDefaultParams";
            this.addDefaultParams.Size = new System.Drawing.Size(138, 17);
            this.addDefaultParams.TabIndex = 15;
            this.addDefaultParams.Text = "Add Default Parameters";
            this.addDefaultParams.UseVisualStyleBackColor = true;
            // 
            // AddEntity_CompositeInstance
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(653, 406);
            this.Controls.Add(this.compositeNameDisplay);
            this.Controls.Add(this.clearSearchBtn);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.searchText);
            this.Controls.Add(this.addDefaultParams);
            this.Controls.Add(this.createEntity);
            this.Controls.Add(this.entityName);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.compositeTree);
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.Name = "AddEntity_CompositeInstance";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Create Composite Instance Entity";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button createEntity;
        private System.Windows.Forms.TextBox entityName;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox searchText;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button clearSearchBtn;
        private System.Windows.Forms.TreeView compositeTree;
        private System.Windows.Forms.ImageList imageList;
        private System.Windows.Forms.TextBox compositeNameDisplay;
        private System.Windows.Forms.CheckBox addDefaultParams;
    }
}

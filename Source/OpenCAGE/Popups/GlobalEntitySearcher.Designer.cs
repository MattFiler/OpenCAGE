namespace CommandsEditor
{
    partial class GlobalEntitySearcher
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
            System.Windows.Forms.ListViewGroup listViewGroup1 = new System.Windows.Forms.ListViewGroup("Parameters", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup2 = new System.Windows.Forms.ListViewGroup("Functions", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup3 = new System.Windows.Forms.ListViewGroup("Composite Instances", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup4 = new System.Windows.Forms.ListViewGroup("Proxies", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup5 = new System.Windows.Forms.ListViewGroup("Aliases", System.Windows.Forms.HorizontalAlignment.Left);
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GlobalEntitySearcher));
            this.label = new System.Windows.Forms.Label();
            this.jumpToEntity = new System.Windows.Forms.Button();
            this.entityVariant = new System.Windows.Forms.ComboBox();
            this.searchFunctionTypes = new System.Windows.Forms.Button();
            this.entityList = new System.Windows.Forms.ListView();
            this.EntityName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.EntityType = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.entityListIcons = new System.Windows.Forms.ImageList(this.components);
            this.nameSearchBox = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // label
            // 
            this.label.AutoSize = true;
            this.label.Location = new System.Drawing.Point(12, 15);
            this.label.Name = "label";
            this.label.Size = new System.Drawing.Size(111, 13);
            this.label.TabIndex = 148;
            this.label.Text = "Find uses of function: ";
            // 
            // jumpToEntity
            // 
            this.jumpToEntity.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.jumpToEntity.Location = new System.Drawing.Point(602, 464);
            this.jumpToEntity.Name = "jumpToEntity";
            this.jumpToEntity.Size = new System.Drawing.Size(147, 23);
            this.jumpToEntity.TabIndex = 150;
            this.jumpToEntity.Text = "Jump To Selected";
            this.jumpToEntity.UseVisualStyleBackColor = true;
            this.jumpToEntity.Click += new System.EventHandler(this.jumpToEntity_Click);
            // 
            // entityVariant
            // 
            this.entityVariant.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.entityVariant.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.entityVariant.FormattingEnabled = true;
            this.entityVariant.Location = new System.Drawing.Point(129, 12);
            this.entityVariant.Name = "entityVariant";
            this.entityVariant.Size = new System.Drawing.Size(486, 21);
            this.entityVariant.TabIndex = 151;
            this.entityVariant.SelectedIndexChanged += new System.EventHandler(this.entityVariant_SelectedIndexChanged);
            // 
            // searchFunctionTypes
            // 
            this.searchFunctionTypes.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.searchFunctionTypes.Location = new System.Drawing.Point(621, 11);
            this.searchFunctionTypes.Name = "searchFunctionTypes";
            this.searchFunctionTypes.Size = new System.Drawing.Size(128, 23);
            this.searchFunctionTypes.TabIndex = 153;
            this.searchFunctionTypes.Text = "Search Types";
            this.searchFunctionTypes.UseVisualStyleBackColor = true;
            this.searchFunctionTypes.Click += new System.EventHandler(this.searchFunctionTypes_Click);
            // 
            // entityList
            // 
            this.entityList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.entityList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.EntityName,
            this.EntityType});
            this.entityList.FullRowSelect = true;
            listViewGroup1.Header = "Parameters";
            listViewGroup1.Name = "Variables";
            listViewGroup2.Header = "Functions";
            listViewGroup2.Name = "Functions";
            listViewGroup3.Header = "Composite Instances";
            listViewGroup3.Name = "Composites";
            listViewGroup4.Header = "Proxies";
            listViewGroup4.Name = "Proxies";
            listViewGroup5.Header = "Aliases";
            listViewGroup5.Name = "Aliases";
            this.entityList.Groups.AddRange(new System.Windows.Forms.ListViewGroup[] {
            listViewGroup1,
            listViewGroup2,
            listViewGroup3,
            listViewGroup4,
            listViewGroup5});
            this.entityList.HideSelection = false;
            this.entityList.LabelWrap = false;
            this.entityList.LargeImageList = this.entityListIcons;
            this.entityList.Location = new System.Drawing.Point(15, 40);
            this.entityList.MultiSelect = false;
            this.entityList.Name = "entityList";
            this.entityList.Size = new System.Drawing.Size(734, 418);
            this.entityList.SmallImageList = this.entityListIcons;
            this.entityList.TabIndex = 177;
            this.entityList.UseCompatibleStateImageBehavior = false;
            this.entityList.View = System.Windows.Forms.View.Details;
            // 
            // EntityName
            // 
            this.EntityName.Text = "Name";
            this.EntityName.Width = 279;
            // 
            // EntityType
            // 
            this.EntityType.Text = "Type";
            this.EntityType.Width = 163;
            // 
            // entityListIcons
            // 
            this.entityListIcons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("entityListIcons.ImageStream")));
            this.entityListIcons.TransparentColor = System.Drawing.Color.Transparent;
            this.entityListIcons.Images.SetKeyName(0, "AnimatorController Icon.png");
            this.entityListIcons.Images.SetKeyName(1, "d_ScriptableObject Icon braces only.png");
            this.entityListIcons.Images.SetKeyName(2, "d_PrefabVariant Icon.png");
            this.entityListIcons.Images.SetKeyName(3, "d_ScriptableObject Icon.png");
            this.entityListIcons.Images.SetKeyName(4, "AreaEffector2D Icon.ico");
            this.entityListIcons.Images.SetKeyName(5, "variable left.png");
            this.entityListIcons.Images.SetKeyName(6, "variable right.png");
            // 
            // nameSearchBox
            // 
            this.nameSearchBox.Location = new System.Drawing.Point(15, 12);
            this.nameSearchBox.Name = "nameSearchBox";
            this.nameSearchBox.Size = new System.Drawing.Size(600, 20);
            this.nameSearchBox.TabIndex = 178;
            this.nameSearchBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.CreateEntityOnEnterKey);
            // 
            // GlobalEntitySearcher
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(761, 499);
            this.Controls.Add(this.nameSearchBox);
            this.Controls.Add(this.entityList);
            this.Controls.Add(this.searchFunctionTypes);
            this.Controls.Add(this.entityVariant);
            this.Controls.Add(this.jumpToEntity);
            this.Controls.Add(this.label);
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.Name = "GlobalEntitySearcher";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Function Uses";
            this.Load += new System.EventHandler(this.GlobalEntitySearcher_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label label;
        private System.Windows.Forms.Button jumpToEntity;
        private System.Windows.Forms.ComboBox entityVariant;
        private System.Windows.Forms.Button searchFunctionTypes;
        private System.Windows.Forms.ListView entityList;
        private System.Windows.Forms.ColumnHeader EntityName;
        private System.Windows.Forms.ColumnHeader EntityType;
        private System.Windows.Forms.ImageList entityListIcons;
        private System.Windows.Forms.TextBox nameSearchBox;
    }
}

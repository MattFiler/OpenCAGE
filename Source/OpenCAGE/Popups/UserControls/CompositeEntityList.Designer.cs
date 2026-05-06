namespace CommandsEditor.Popups.UserControls
{
    partial class CompositeEntityList
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CompositeEntityList));
            System.Windows.Forms.ListViewGroup listViewGroup1 = new System.Windows.Forms.ListViewGroup("Parameters", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup2 = new System.Windows.Forms.ListViewGroup("Functions", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup3 = new System.Windows.Forms.ListViewGroup("Composite Instances", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup4 = new System.Windows.Forms.ListViewGroup("Proxies", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup5 = new System.Windows.Forms.ListViewGroup("Aliases", System.Windows.Forms.HorizontalAlignment.Left);
            this.panel1 = new System.Windows.Forms.Panel();
            this.clearSearchBtn = new System.Windows.Forms.Button();
            this.composite_content = new System.Windows.Forms.ListView();
            this.EntityName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.EntityType = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.entityListIcons = new System.Windows.Forms.ImageList(this.components);
            this.entity_search_box = new System.Windows.Forms.TextBox();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.clearSearchBtn);
            this.panel1.Controls.Add(this.composite_content);
            this.panel1.Controls.Add(this.entity_search_box);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(830, 773);
            this.panel1.TabIndex = 181;
            // 
            // clearSearchBtn
            // 
            this.clearSearchBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.clearSearchBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.clearSearchBtn.Image = ((System.Drawing.Image)(resources.GetObject("clearSearchBtn.Image")));
            this.clearSearchBtn.Location = new System.Drawing.Point(810, 4);
            this.clearSearchBtn.Name = "clearSearchBtn";
            this.clearSearchBtn.Size = new System.Drawing.Size(20, 20);
            this.clearSearchBtn.TabIndex = 177;
            this.clearSearchBtn.UseVisualStyleBackColor = true;
            this.clearSearchBtn.Click += new System.EventHandler(this.clearSearchBtn_Click);
            // 
            // composite_content
            // 
            this.composite_content.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.composite_content.CheckBoxes = true;
            this.composite_content.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.EntityName,
            this.EntityType});
            this.composite_content.FullRowSelect = true;
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
            this.composite_content.Groups.AddRange(new System.Windows.Forms.ListViewGroup[] {
            listViewGroup1,
            listViewGroup2,
            listViewGroup3,
            listViewGroup4,
            listViewGroup5});
            this.composite_content.HideSelection = false;
            this.composite_content.LabelWrap = false;
            this.composite_content.Location = new System.Drawing.Point(3, 30);
            this.composite_content.MultiSelect = false;
            this.composite_content.Name = "composite_content";
            this.composite_content.Size = new System.Drawing.Size(827, 743);
            this.composite_content.SmallImageList = this.entityListIcons;
            this.composite_content.TabIndex = 176;
            this.composite_content.UseCompatibleStateImageBehavior = false;
            this.composite_content.View = System.Windows.Forms.View.Details;
            this.composite_content.SelectedIndexChanged += new System.EventHandler(this.composite_content_SelectedIndexChanged);
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
            // entity_search_box
            // 
            this.entity_search_box.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.entity_search_box.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.entity_search_box.Location = new System.Drawing.Point(3, 4);
            this.entity_search_box.Name = "entity_search_box";
            this.entity_search_box.Size = new System.Drawing.Size(827, 20);
            this.entity_search_box.TabIndex = 146;
            this.entity_search_box.TextChanged += new System.EventHandler(this.entity_search_box_TextChanged);
            // 
            // CompositeEntityList
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panel1);
            this.Name = "CompositeEntityList";
            this.Size = new System.Drawing.Size(830, 773);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ListView composite_content;
        private System.Windows.Forms.ColumnHeader EntityName;
        private System.Windows.Forms.ColumnHeader EntityType;
        private System.Windows.Forms.TextBox entity_search_box;
        private System.Windows.Forms.ImageList entityListIcons;
        private System.Windows.Forms.Button clearSearchBtn;
    }
}

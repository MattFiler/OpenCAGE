namespace OpenCAGE
{
    partial class ShowCrossRefs
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ShowCrossRefs));
            this.label = new System.Windows.Forms.Label();
            this.jumpToEntity = new System.Windows.Forms.Button();
            this.showLinkedProxies = new System.Windows.Forms.Button();
            this.showLinkedOverrides = new System.Windows.Forms.Button();
            this.showLinkedTriggerSequences = new System.Windows.Forms.Button();
            this.showLinkedCageAnimations = new System.Windows.Forms.Button();
            this.entityList = new System.Windows.Forms.ListView();
            this.EntityName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.EntityType = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.entityListIcons = new System.Windows.Forms.ImageList(this.components);
            this.showFlowgraphs = new System.Windows.Forms.Button();
            this.flowgraphList = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.SuspendLayout();
            // 
            // label
            // 
            this.label.AutoSize = true;
            this.label.Location = new System.Drawing.Point(129, 11);
            this.label.Name = "label";
            this.label.Size = new System.Drawing.Size(139, 13);
            this.label.TabIndex = 148;
            this.label.Text = "Nodes created for this entity";
            // 
            // jumpToEntity
            // 
            this.jumpToEntity.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.jumpToEntity.Location = new System.Drawing.Point(724, 416);
            this.jumpToEntity.Name = "jumpToEntity";
            this.jumpToEntity.Size = new System.Drawing.Size(147, 23);
            this.jumpToEntity.TabIndex = 150;
            this.jumpToEntity.Text = "Jump To Selected";
            this.jumpToEntity.UseVisualStyleBackColor = true;
            this.jumpToEntity.Click += new System.EventHandler(this.jumpToEntity_Click);
            // 
            // showLinkedProxies
            // 
            this.showLinkedProxies.Location = new System.Drawing.Point(12, 76);
            this.showLinkedProxies.Name = "showLinkedProxies";
            this.showLinkedProxies.Size = new System.Drawing.Size(114, 41);
            this.showLinkedProxies.TabIndex = 151;
            this.showLinkedProxies.Text = "Proxies";
            this.showLinkedProxies.UseVisualStyleBackColor = true;
            this.showLinkedProxies.Click += new System.EventHandler(this.showLinkedProxies_Click);
            // 
            // showLinkedOverrides
            // 
            this.showLinkedOverrides.Location = new System.Drawing.Point(12, 123);
            this.showLinkedOverrides.Name = "showLinkedOverrides";
            this.showLinkedOverrides.Size = new System.Drawing.Size(114, 41);
            this.showLinkedOverrides.TabIndex = 152;
            this.showLinkedOverrides.Text = "Aliases";
            this.showLinkedOverrides.UseVisualStyleBackColor = true;
            this.showLinkedOverrides.Click += new System.EventHandler(this.showLinkedOverrides_Click);
            // 
            // showLinkedTriggerSequences
            // 
            this.showLinkedTriggerSequences.Location = new System.Drawing.Point(12, 170);
            this.showLinkedTriggerSequences.Name = "showLinkedTriggerSequences";
            this.showLinkedTriggerSequences.Size = new System.Drawing.Size(114, 41);
            this.showLinkedTriggerSequences.TabIndex = 153;
            this.showLinkedTriggerSequences.Text = "TriggerSequences";
            this.showLinkedTriggerSequences.UseVisualStyleBackColor = true;
            this.showLinkedTriggerSequences.Click += new System.EventHandler(this.showLinkedTriggerSequences_Click);
            // 
            // showLinkedCageAnimations
            // 
            this.showLinkedCageAnimations.Location = new System.Drawing.Point(12, 217);
            this.showLinkedCageAnimations.Name = "showLinkedCageAnimations";
            this.showLinkedCageAnimations.Size = new System.Drawing.Size(114, 41);
            this.showLinkedCageAnimations.TabIndex = 154;
            this.showLinkedCageAnimations.Text = "CAGEAnimations";
            this.showLinkedCageAnimations.UseVisualStyleBackColor = true;
            this.showLinkedCageAnimations.Click += new System.EventHandler(this.showLinkedCageAnimations_Click);
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
            this.entityList.Location = new System.Drawing.Point(132, 29);
            this.entityList.MultiSelect = false;
            this.entityList.Name = "entityList";
            this.entityList.Size = new System.Drawing.Size(738, 381);
            this.entityList.SmallImageList = this.entityListIcons;
            this.entityList.TabIndex = 178;
            this.entityList.UseCompatibleStateImageBehavior = false;
            this.entityList.View = System.Windows.Forms.View.Details;
            // 
            // EntityName
            // 
            this.EntityName.Text = "Name";
            this.EntityName.Width = 509;
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
            // showFlowgraphs
            // 
            this.showFlowgraphs.Location = new System.Drawing.Point(12, 29);
            this.showFlowgraphs.Name = "showFlowgraphs";
            this.showFlowgraphs.Size = new System.Drawing.Size(114, 41);
            this.showFlowgraphs.TabIndex = 179;
            this.showFlowgraphs.Text = "Flowgraphs";
            this.showFlowgraphs.UseVisualStyleBackColor = true;
            this.showFlowgraphs.Click += new System.EventHandler(this.showFlowgraphs_Click);
            // 
            // flowgraphList
            // 
            this.flowgraphList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.flowgraphList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2});
            this.flowgraphList.FullRowSelect = true;
            this.flowgraphList.GridLines = true;
            this.flowgraphList.HideSelection = false;
            this.flowgraphList.LabelWrap = false;
            this.flowgraphList.Location = new System.Drawing.Point(132, 29);
            this.flowgraphList.MultiSelect = false;
            this.flowgraphList.Name = "flowgraphList";
            this.flowgraphList.ShowGroups = false;
            this.flowgraphList.Size = new System.Drawing.Size(738, 381);
            this.flowgraphList.TabIndex = 180;
            this.flowgraphList.UseCompatibleStateImageBehavior = false;
            this.flowgraphList.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "Flowgraph Name";
            this.columnHeader1.Width = 417;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "Number Of Nodes";
            this.columnHeader2.Width = 170;
            // 
            // ShowCrossRefs
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(882, 448);
            this.Controls.Add(this.flowgraphList);
            this.Controls.Add(this.showFlowgraphs);
            this.Controls.Add(this.entityList);
            this.Controls.Add(this.showLinkedCageAnimations);
            this.Controls.Add(this.showLinkedTriggerSequences);
            this.Controls.Add(this.showLinkedOverrides);
            this.Controls.Add(this.showLinkedProxies);
            this.Controls.Add(this.jumpToEntity);
            this.Controls.Add(this.label);
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.Name = "ShowCrossRefs";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Entity References";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label label;
        private System.Windows.Forms.Button jumpToEntity;
        private System.Windows.Forms.Button showLinkedProxies;
        private System.Windows.Forms.Button showLinkedOverrides;
        private System.Windows.Forms.Button showLinkedTriggerSequences;
        private System.Windows.Forms.Button showLinkedCageAnimations;
        private System.Windows.Forms.ListView entityList;
        private System.Windows.Forms.ColumnHeader EntityName;
        private System.Windows.Forms.ColumnHeader EntityType;
        private System.Windows.Forms.ImageList entityListIcons;
        private System.Windows.Forms.Button showFlowgraphs;
        private System.Windows.Forms.ListView flowgraphList;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
    }
}

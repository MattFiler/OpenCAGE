namespace OpenCAGE.DockPanels
{
    partial class CompositeDisplay
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CompositeDisplay));
            this.entityListIcons = new System.Windows.Forms.ImageList(this.components);
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.createEntity = new System.Windows.Forms.ToolStripDropDownButton();
            this.createVariableEntityToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createFunctionEntityToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createCompositeEntityToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createProxyEntityToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createAliasToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportComposite = new System.Windows.Forms.ToolStripButton();
            this.findUses = new System.Windows.Forms.ToolStripButton();
            this.deleteCheckedEntities = new System.Windows.Forms.ToolStripButton();
            this.renameComposite = new System.Windows.Forms.ToolStripButton();
            this.deleteComposite = new System.Windows.Forms.ToolStripButton();
            this.createFlowgraph = new System.Windows.Forms.ToolStripButton();
            this.show3DPreview = new System.Windows.Forms.ToolStripButton();
            this.dockPanel = new WeifenLuo.WinFormsUI.Docking.DockPanel();
            this.vS2015DarkTheme1 = new WeifenLuo.WinFormsUI.Docking.VS2015DarkTheme();
            this.vS2015BlueTheme1 = new WeifenLuo.WinFormsUI.Docking.VS2015BlueTheme();
            this.imageList = new System.Windows.Forms.ImageList(this.components);
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.closeSelected = new System.Windows.Forms.ToolStripMenuItem();
            this.goBackOnPath = new System.Windows.Forms.Button();
            this.pathDisplay = new System.Windows.Forms.TextBox();
            this.instanceInfo = new System.Windows.Forms.Button();
            this.toolStrip1.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
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
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.createEntity,
            this.exportComposite,
            this.findUses,
            this.show3DPreview,
            this.deleteCheckedEntities,
            this.renameComposite,
            this.deleteComposite,
            this.createFlowgraph});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(1615, 25);
            this.toolStrip1.TabIndex = 177;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // createEntity
            // 
            this.createEntity.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.createVariableEntityToolStripMenuItem,
            this.createFunctionEntityToolStripMenuItem,
            this.createCompositeEntityToolStripMenuItem,
            this.createProxyEntityToolStripMenuItem,
            this.createAliasToolStripMenuItem});
            this.createEntity.Image = ((System.Drawing.Image)(resources.GetObject("createEntity.Image")));
            this.createEntity.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.createEntity.Name = "createEntity";
            this.createEntity.Size = new System.Drawing.Size(103, 22);
            this.createEntity.Text = "Create Entity";
            // 
            // createVariableEntityToolStripMenuItem
            // 
            this.createVariableEntityToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("createVariableEntityToolStripMenuItem.Image")));
            this.createVariableEntityToolStripMenuItem.Name = "createVariableEntityToolStripMenuItem";
            this.createVariableEntityToolStripMenuItem.Size = new System.Drawing.Size(230, 22);
            this.createVariableEntityToolStripMenuItem.Text = "Create Parameter";
            this.createVariableEntityToolStripMenuItem.ToolTipText = "Creates an entity which acts as a parameter that can be accessed when instancing " +
    "this composite.";
            this.createVariableEntityToolStripMenuItem.Click += new System.EventHandler(this.createVariableEntityToolStripMenuItem_Click);
            // 
            // createFunctionEntityToolStripMenuItem
            // 
            this.createFunctionEntityToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("createFunctionEntityToolStripMenuItem.Image")));
            this.createFunctionEntityToolStripMenuItem.Name = "createFunctionEntityToolStripMenuItem";
            this.createFunctionEntityToolStripMenuItem.Size = new System.Drawing.Size(230, 22);
            this.createFunctionEntityToolStripMenuItem.Text = "Create Function";
            this.createFunctionEntityToolStripMenuItem.ToolTipText = "Create an entity that can execute a Cathode function.";
            this.createFunctionEntityToolStripMenuItem.Click += new System.EventHandler(this.createFunctionEntityToolStripMenuItem_Click);
            // 
            // createCompositeEntityToolStripMenuItem
            // 
            this.createCompositeEntityToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("createCompositeEntityToolStripMenuItem.Image")));
            this.createCompositeEntityToolStripMenuItem.Name = "createCompositeEntityToolStripMenuItem";
            this.createCompositeEntityToolStripMenuItem.Size = new System.Drawing.Size(230, 22);
            this.createCompositeEntityToolStripMenuItem.Text = "Create Instance of Composite";
            this.createCompositeEntityToolStripMenuItem.ToolTipText = "Create an entity that instances another composite.";
            this.createCompositeEntityToolStripMenuItem.Click += new System.EventHandler(this.createCompositeEntityToolStripMenuItem_Click);
            // 
            // createProxyEntityToolStripMenuItem
            // 
            this.createProxyEntityToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("createProxyEntityToolStripMenuItem.Image")));
            this.createProxyEntityToolStripMenuItem.Name = "createProxyEntityToolStripMenuItem";
            this.createProxyEntityToolStripMenuItem.Size = new System.Drawing.Size(230, 22);
            this.createProxyEntityToolStripMenuItem.Text = "Create Proxy";
            this.createProxyEntityToolStripMenuItem.ToolTipText = "Create an entity that acts as a proxy to an entity in another composite.";
            this.createProxyEntityToolStripMenuItem.Click += new System.EventHandler(this.createProxyEntityToolStripMenuItem_Click);
            // 
            // createAliasToolStripMenuItem
            // 
            this.createAliasToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("createAliasToolStripMenuItem.Image")));
            this.createAliasToolStripMenuItem.Name = "createAliasToolStripMenuItem";
            this.createAliasToolStripMenuItem.Size = new System.Drawing.Size(230, 22);
            this.createAliasToolStripMenuItem.Text = "Create Alias";
            this.createAliasToolStripMenuItem.Click += new System.EventHandler(this.createAliasToolStripMenuItem_Click);
            // 
            // exportComposite
            // 
            this.exportComposite.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.exportComposite.Image = ((System.Drawing.Image)(resources.GetObject("exportComposite.Image")));
            this.exportComposite.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.exportComposite.Name = "exportComposite";
            this.exportComposite.Size = new System.Drawing.Size(110, 22);
            this.exportComposite.Text = "Port Composite";
            this.exportComposite.Click += new System.EventHandler(this.exportComposite_Click);
            // 
            // findUses
            // 
            this.findUses.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.findUses.Image = ((System.Drawing.Image)(resources.GetObject("findUses.Image")));
            this.findUses.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.findUses.Name = "findUses";
            this.findUses.Size = new System.Drawing.Size(177, 22);
            this.findUses.Text = "Find Instances of Composite";
            this.findUses.Click += new System.EventHandler(this.findUses_Click);
            // 
            // deleteCheckedEntities
            // 
            this.deleteCheckedEntities.Image = ((System.Drawing.Image)(resources.GetObject("deleteCheckedEntities.Image")));
            this.deleteCheckedEntities.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.deleteCheckedEntities.Name = "deleteCheckedEntities";
            this.deleteCheckedEntities.Size = new System.Drawing.Size(150, 22);
            this.deleteCheckedEntities.Text = "Delete Checked Entities";
            this.deleteCheckedEntities.Click += new System.EventHandler(this.deleteCheckedEntities_Click);
            // 
            // renameComposite
            // 
            this.renameComposite.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.renameComposite.Image = ((System.Drawing.Image)(resources.GetObject("renameComposite.Image")));
            this.renameComposite.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.renameComposite.Name = "renameComposite";
            this.renameComposite.Size = new System.Drawing.Size(131, 22);
            this.renameComposite.Text = "Rename Composite";
            this.renameComposite.Click += new System.EventHandler(this.renameComposite_Click);
            // 
            // deleteComposite
            // 
            this.deleteComposite.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.deleteComposite.Image = ((System.Drawing.Image)(resources.GetObject("deleteComposite.Image")));
            this.deleteComposite.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.deleteComposite.Name = "deleteComposite";
            this.deleteComposite.Size = new System.Drawing.Size(121, 22);
            this.deleteComposite.Text = "Delete Composite";
            this.deleteComposite.Click += new System.EventHandler(this.deleteComposite_Click);
            // 
            // createFlowgraph
            // 
            this.createFlowgraph.Image = ((System.Drawing.Image)(resources.GetObject("createFlowgraph.Image")));
            this.createFlowgraph.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.createFlowgraph.Name = "createFlowgraph";
            this.createFlowgraph.Size = new System.Drawing.Size(120, 22);
            this.createFlowgraph.Text = "Create Flowgraph";
            this.createFlowgraph.Click += new System.EventHandler(this.createFlowgraph_Click);
            // 
            // show3DPreview
            // 
            this.show3DPreview.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.show3DPreview.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.show3DPreview.Name = "show3DPreview";
            this.show3DPreview.Size = new System.Drawing.Size(68, 22);
            this.show3DPreview.Text = "3D Preview";
            this.show3DPreview.ToolTipText = "Open a 3D preview of the current composite.";
            this.show3DPreview.Click += new System.EventHandler(this.show3DPreview_Click);
            // 
            // dockPanel
            // 
            this.dockPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dockPanel.DockBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(41)))), ((int)(((byte)(57)))), ((int)(((byte)(85)))));
            this.dockPanel.Location = new System.Drawing.Point(0, 25);
            this.dockPanel.Name = "dockPanel";
            this.dockPanel.Padding = new System.Windows.Forms.Padding(6);
            this.dockPanel.ShowAutoHideContentOnHover = false;
            this.dockPanel.Size = new System.Drawing.Size(1615, 773);
            this.dockPanel.TabIndex = 178;
            this.dockPanel.Theme = this.vS2015BlueTheme1;
            // 
            // imageList
            // 
            this.imageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList.ImageStream")));
            this.imageList.TransparentColor = System.Drawing.Color.Magenta;
            this.imageList.Images.SetKeyName(0, "flag_blue");
            this.imageList.Images.SetKeyName(1, "flag_green");
            this.imageList.Images.SetKeyName(2, "flag_red");
            this.imageList.Images.SetKeyName(3, "behavior");
            this.imageList.Images.SetKeyName(4, "behavior_loaded");
            this.imageList.Images.SetKeyName(5, "behavior_modified");
            this.imageList.Images.SetKeyName(6, "condition");
            this.imageList.Images.SetKeyName(7, "impulse");
            this.imageList.Images.SetKeyName(8, "action");
            this.imageList.Images.SetKeyName(9, "decorator");
            this.imageList.Images.SetKeyName(10, "sequence");
            this.imageList.Images.SetKeyName(11, "selector");
            this.imageList.Images.SetKeyName(12, "parallel");
            this.imageList.Images.SetKeyName(13, "folder_closed");
            this.imageList.Images.SetKeyName(14, "folder_open");
            this.imageList.Images.SetKeyName(15, "event");
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.closeSelected});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(181, 48);
            // 
            // closeSelected
            // 
            this.closeSelected.Image = ((System.Drawing.Image)(resources.GetObject("closeSelected.Image")));
            this.closeSelected.Name = "closeSelected";
            this.closeSelected.Size = new System.Drawing.Size(180, 22);
            this.closeSelected.Text = "Close Composite";
            this.closeSelected.Click += new System.EventHandler(this.closeSelected_Click);
            // 
            // goBackOnPath
            // 
            this.goBackOnPath.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.goBackOnPath.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.goBackOnPath.Location = new System.Drawing.Point(3, 801);
            this.goBackOnPath.Name = "goBackOnPath";
            this.goBackOnPath.Size = new System.Drawing.Size(62, 20);
            this.goBackOnPath.TabIndex = 177;
            this.goBackOnPath.Text = "< Back";
            this.goBackOnPath.UseVisualStyleBackColor = true;
            this.goBackOnPath.Click += new System.EventHandler(this.goBackOnPath_Click);
            // 
            // pathDisplay
            // 
            this.pathDisplay.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pathDisplay.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pathDisplay.Enabled = false;
            this.pathDisplay.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.pathDisplay.Location = new System.Drawing.Point(64, 801);
            this.pathDisplay.Name = "pathDisplay";
            this.pathDisplay.ReadOnly = true;
            this.pathDisplay.Size = new System.Drawing.Size(1406, 20);
            this.pathDisplay.TabIndex = 177;
            // 
            // instanceInfo
            // 
            this.instanceInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.instanceInfo.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.instanceInfo.Location = new System.Drawing.Point(1469, 801);
            this.instanceInfo.Name = "instanceInfo";
            this.instanceInfo.Size = new System.Drawing.Size(146, 20);
            this.instanceInfo.TabIndex = 183;
            this.instanceInfo.Text = "Composite Instance Info";
            this.instanceInfo.UseVisualStyleBackColor = true;
            this.instanceInfo.Click += new System.EventHandler(this.instanceInfo_Click);
            // 
            // CompositeDisplay
            // 
            this.AllowEndUserDocking = false;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1615, 821);
            this.Controls.Add(this.dockPanel);
            this.Controls.Add(this.instanceInfo);
            this.Controls.Add(this.pathDisplay);
            this.Controls.Add(this.goBackOnPath);
            this.Controls.Add(this.toolStrip1);
            this.DockAreas = ((WeifenLuo.WinFormsUI.Docking.DockAreas)((WeifenLuo.WinFormsUI.Docking.DockAreas.Float | WeifenLuo.WinFormsUI.Docking.DockAreas.Document)));
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.Name = "CompositeDisplay";
            this.ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.Document;
            this.TabPageContextMenuStrip = this.contextMenuStrip1;
            this.Text = "Selected Composite";
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton findUses;
        private System.Windows.Forms.ToolStripButton deleteComposite;
        private WeifenLuo.WinFormsUI.Docking.DockPanel dockPanel;
        private WeifenLuo.WinFormsUI.Docking.VS2015DarkTheme vS2015DarkTheme1;
        private WeifenLuo.WinFormsUI.Docking.VS2015BlueTheme vS2015BlueTheme1;
        private System.Windows.Forms.ToolStripButton deleteCheckedEntities;
        private System.Windows.Forms.ToolStripButton exportComposite;
        private System.Windows.Forms.ImageList imageList;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripDropDownButton createEntity;
        private System.Windows.Forms.ToolStripMenuItem createVariableEntityToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createFunctionEntityToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createCompositeEntityToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createProxyEntityToolStripMenuItem;
        private System.Windows.Forms.Button goBackOnPath;
        private System.Windows.Forms.TextBox pathDisplay;
        private System.Windows.Forms.ToolStripMenuItem closeSelected;
        private System.Windows.Forms.ImageList entityListIcons;
        private System.Windows.Forms.ToolStripMenuItem createAliasToolStripMenuItem;
        private System.Windows.Forms.Button instanceInfo;
        private System.Windows.Forms.ToolStripButton renameComposite;
        private System.Windows.Forms.ToolStripButton createFlowgraph;
        private System.Windows.Forms.ToolStripButton show3DPreview;
    }
}

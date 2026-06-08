namespace OpenCAGE.DockPanels
{
    partial class EntityList
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EntityList));
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            this.compositeEntityList1 = new Popups.UserControls.CompositeEntityList();
            this.EntityListContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.createToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createParameterToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createFunctionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createInstanceOfCompositeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createProxyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createAliasToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.deleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.renameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.duplicateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.copyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pasteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.findReferencesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.deleteCheckedEntities = new System.Windows.Forms.ToolStripButton();
            this.EntityListContextMenu.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // imageList1
            // 
            this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
            this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList1.Images.SetKeyName(0, "GenericEditor.ico");
            // 
            // compositeEntityList1
            // 
            this.compositeEntityList1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.compositeEntityList1.Location = new System.Drawing.Point(0, 0);
            this.compositeEntityList1.Name = "compositeEntityList1";
            this.compositeEntityList1.Size = new System.Drawing.Size(479, 780);
            this.compositeEntityList1.TabIndex = 1;
            // 
            // EntityListContextMenu
            // 
            this.EntityListContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.createToolStripMenuItem,
            this.toolStripSeparator1,
            this.deleteToolStripMenuItem,
            this.renameToolStripMenuItem,
            this.duplicateToolStripMenuItem,
            this.toolStripSeparator2,
            this.copyToolStripMenuItem,
            this.pasteToolStripMenuItem,
            this.toolStripSeparator3,
            this.findReferencesToolStripMenuItem});
            this.EntityListContextMenu.Name = "EntityListContextMenu";
            this.EntityListContextMenu.Size = new System.Drawing.Size(181, 198);
            this.EntityListContextMenu.Opening += new System.ComponentModel.CancelEventHandler(this.EntityListContextMenu_Opening);
            // 
            // createToolStripMenuItem
            // 
            this.createToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.createParameterToolStripMenuItem,
            this.createFunctionToolStripMenuItem,
            this.createInstanceOfCompositeToolStripMenuItem,
            this.createProxyToolStripMenuItem,
            this.createAliasToolStripMenuItem1});
            this.createToolStripMenuItem.Name = "createToolStripMenuItem";
            this.createToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.createToolStripMenuItem.Text = "Create Entity";
            // 
            // createParameterToolStripMenuItem
            // 
            this.createParameterToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("createParameterToolStripMenuItem.Image")));
            this.createParameterToolStripMenuItem.Name = "createParameterToolStripMenuItem";
            this.createParameterToolStripMenuItem.Size = new System.Drawing.Size(220, 22);
            this.createParameterToolStripMenuItem.Text = "New Parameter";
            this.createParameterToolStripMenuItem.Click += new System.EventHandler(this.createParameterToolStripMenuItem_Click);
            // 
            // createFunctionToolStripMenuItem
            // 
            this.createFunctionToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("createFunctionToolStripMenuItem.Image")));
            this.createFunctionToolStripMenuItem.Name = "createFunctionToolStripMenuItem";
            this.createFunctionToolStripMenuItem.Size = new System.Drawing.Size(220, 22);
            this.createFunctionToolStripMenuItem.Text = "New Function";
            this.createFunctionToolStripMenuItem.Click += new System.EventHandler(this.createFunctionToolStripMenuItem_Click);
            // 
            // createInstanceOfCompositeToolStripMenuItem
            // 
            this.createInstanceOfCompositeToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("createInstanceOfCompositeToolStripMenuItem.Image")));
            this.createInstanceOfCompositeToolStripMenuItem.Name = "createInstanceOfCompositeToolStripMenuItem";
            this.createInstanceOfCompositeToolStripMenuItem.Size = new System.Drawing.Size(220, 22);
            this.createInstanceOfCompositeToolStripMenuItem.Text = "New Instance of Composite";
            this.createInstanceOfCompositeToolStripMenuItem.Click += new System.EventHandler(this.createInstanceOfCompositeToolStripMenuItem_Click);
            // 
            // createProxyToolStripMenuItem
            // 
            this.createProxyToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("createProxyToolStripMenuItem.Image")));
            this.createProxyToolStripMenuItem.Name = "createProxyToolStripMenuItem";
            this.createProxyToolStripMenuItem.Size = new System.Drawing.Size(220, 22);
            this.createProxyToolStripMenuItem.Text = "New Proxy";
            this.createProxyToolStripMenuItem.Click += new System.EventHandler(this.createProxyToolStripMenuItem_Click);
            // 
            // createAliasToolStripMenuItem1
            // 
            this.createAliasToolStripMenuItem1.Image = ((System.Drawing.Image)(resources.GetObject("createAliasToolStripMenuItem1.Image")));
            this.createAliasToolStripMenuItem1.Name = "createAliasToolStripMenuItem1";
            this.createAliasToolStripMenuItem1.Size = new System.Drawing.Size(220, 22);
            this.createAliasToolStripMenuItem1.Text = "New Alias";
            this.createAliasToolStripMenuItem1.Click += new System.EventHandler(this.createAliasToolStripMenuItem1_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(177, 6);
            // 
            // deleteToolStripMenuItem
            // 
            this.deleteToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("deleteToolStripMenuItem.Image")));
            this.deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
            this.deleteToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.deleteToolStripMenuItem.Text = "Delete";
            this.deleteToolStripMenuItem.Click += new System.EventHandler(this.deleteToolStripMenuItem_Click);
            // 
            // renameToolStripMenuItem
            // 
            this.renameToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("renameToolStripMenuItem.Image")));
            this.renameToolStripMenuItem.Name = "renameToolStripMenuItem";
            this.renameToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.renameToolStripMenuItem.Text = "Rename";
            this.renameToolStripMenuItem.Click += new System.EventHandler(this.renameToolStripMenuItem_Click);
            // 
            // duplicateToolStripMenuItem
            // 
            this.duplicateToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("duplicateToolStripMenuItem.Image")));
            this.duplicateToolStripMenuItem.Name = "duplicateToolStripMenuItem";
            this.duplicateToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.duplicateToolStripMenuItem.Text = "Duplicate";
            this.duplicateToolStripMenuItem.Click += new System.EventHandler(this.duplicateToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(177, 6);
            this.toolStripSeparator2.Visible = false;
            // 
            // copyToolStripMenuItem
            // 
            this.copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            this.copyToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.copyToolStripMenuItem.Text = "Copy";
            this.copyToolStripMenuItem.Visible = false;
            // 
            // pasteToolStripMenuItem
            // 
            this.pasteToolStripMenuItem.Name = "pasteToolStripMenuItem";
            this.pasteToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.pasteToolStripMenuItem.Text = "Paste";
            this.pasteToolStripMenuItem.Visible = false;
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(177, 6);
            // 
            // findReferencesToolStripMenuItem
            // 
            this.findReferencesToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("findReferencesToolStripMenuItem.Image")));
            this.findReferencesToolStripMenuItem.Name = "findReferencesToolStripMenuItem";
            this.findReferencesToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.findReferencesToolStripMenuItem.Text = "Find References";
            this.findReferencesToolStripMenuItem.Click += new System.EventHandler(this.findReferencesToolStripMenuItem_Click);
            // 
            // toolStrip1
            // 
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.Top;
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.deleteCheckedEntities});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(479, 25);
            this.toolStrip1.TabIndex = 2;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // deleteCheckedEntities
            // 
            this.deleteCheckedEntities.Image = ((System.Drawing.Image)(resources.GetObject("deleteToolStripMenuItem.Image")));
            this.deleteCheckedEntities.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.deleteCheckedEntities.Name = "deleteCheckedEntities";
            this.deleteCheckedEntities.Size = new System.Drawing.Size(150, 22);
            this.deleteCheckedEntities.Text = "Delete Checked Entities";
            this.deleteCheckedEntities.Click += new System.EventHandler(this.deleteCheckedEntities_Click);
            // 
            // EntityList
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(479, 780);
            this.CloseButton = false;
            this.CloseButtonVisible = false;
            this.Controls.Add(this.compositeEntityList1);
            this.Controls.Add(this.toolStrip1);
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.DockAreas = ((WeifenLuo.WinFormsUI.Docking.DockAreas)((WeifenLuo.WinFormsUI.Docking.DockAreas.Float | WeifenLuo.WinFormsUI.Docking.DockAreas.DockLeft)));
            this.Name = "EntityList";
            this.ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.DockLeft;
            this.Text = "Entities";
            this.EntityListContextMenu.ResumeLayout(false);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ImageList imageList1;
        private Popups.UserControls.CompositeEntityList compositeEntityList1;
        private System.Windows.Forms.ContextMenuStrip EntityListContextMenu;
        private System.Windows.Forms.ToolStripMenuItem createToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createParameterToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createFunctionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createInstanceOfCompositeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createProxyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createAliasToolStripMenuItem1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem deleteToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem renameToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem duplicateToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem copyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pasteToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem findReferencesToolStripMenuItem;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton deleteCheckedEntities;
    }
}

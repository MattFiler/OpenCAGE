using System.Drawing;
using System.Windows.Forms;

namespace OpenCAGE.DockPanels
{
    partial class CompositeBrowser
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CompositeBrowser));
            this.imageList = new System.Windows.Forms.ImageList(this.components);
            this.entity_search_box = new System.Windows.Forms.TextBox();
            this.entity_search_btn = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.listView1 = new System.Windows.Forms.ListView();
            this.FileBrowserImageListLarge = new System.Windows.Forms.ImageList(this.components);
            this.FileBrowserImageListSmall = new System.Windows.Forms.ImageList(this.components);
            this.FileBrowserContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.createToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.compositeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.folderToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.deleteFolderToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.renameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.viewModeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.largeIconsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.listToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.treeView1 = new System.Windows.Forms.TreeView();
            this.FileTreeContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.pathDisplay = new System.Windows.Forms.TextBox();
            this.goBackOnPath = new System.Windows.Forms.Button();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.createComposite = new System.Windows.Forms.ToolStripButton();
            this.createFolder = new System.Windows.Forms.ToolStripButton();
            this.FileTreeContextMenuNew = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem3 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripMenuItem4 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem5 = new System.Windows.Forms.ToolStripMenuItem();
            this.FileBrowserContextMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.FileTreeContextMenuNew.SuspendLayout();
            this.SuspendLayout();
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
            // entity_search_box
            // 
            this.entity_search_box.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.entity_search_box.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.entity_search_box.Location = new System.Drawing.Point(0, 0);
            this.entity_search_box.Name = "entity_search_box";
            this.entity_search_box.Size = new System.Drawing.Size(262, 20);
            this.entity_search_box.TabIndex = 159;
            this.entity_search_box.KeyDown += new System.Windows.Forms.KeyEventHandler(this.entity_search_box_KeyDown);
            // 
            // entity_search_btn
            // 
            this.entity_search_btn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.entity_search_btn.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.entity_search_btn.Location = new System.Drawing.Point(261, 0);
            this.entity_search_btn.Name = "entity_search_btn";
            this.entity_search_btn.Size = new System.Drawing.Size(63, 20);
            this.entity_search_btn.TabIndex = 158;
            this.entity_search_btn.Text = "Search";
            this.entity_search_btn.UseVisualStyleBackColor = true;
            this.entity_search_btn.Click += new System.EventHandler(this.entity_search_btn_Click);
            // 
            // listView1
            // 
            this.listView1.Activation = System.Windows.Forms.ItemActivation.TwoClick;
            this.listView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listView1.HideSelection = false;
            this.listView1.LargeImageList = this.FileBrowserImageListLarge;
            this.listView1.Location = new System.Drawing.Point(0, 19);
            this.listView1.MultiSelect = false;
            this.listView1.Name = "listView1";
            this.listView1.Size = new System.Drawing.Size(1175, 687);
            this.listView1.SmallImageList = this.FileBrowserImageListSmall;
            this.listView1.TabIndex = 179;
            this.listView1.UseCompatibleStateImageBehavior = false;
            this.listView1.ItemActivate += new System.EventHandler(this.listView1_SelectedIndexChanged);
            this.listView1.MouseDown += new System.Windows.Forms.MouseEventHandler(this.FooListView_MouseDown);
            // 
            // FileBrowserImageListLarge
            // 
            this.FileBrowserImageListLarge.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("FileBrowserImageListLarge.ImageStream")));
            this.FileBrowserImageListLarge.TransparentColor = System.Drawing.Color.Transparent;
            this.FileBrowserImageListLarge.Images.SetKeyName(0, "temp_prefab.png");
            this.FileBrowserImageListLarge.Images.SetKeyName(1, "temp_folder.png");
            this.FileBrowserImageListLarge.Images.SetKeyName(2, "temp_globe.png");
            this.FileBrowserImageListLarge.Images.SetKeyName(3, "temp_gamemanager.png");
            this.FileBrowserImageListLarge.Images.SetKeyName(4, "temp_displaymodel.png");
            // 
            // FileBrowserImageListSmall
            // 
            this.FileBrowserImageListSmall.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("FileBrowserImageListSmall.ImageStream")));
            this.FileBrowserImageListSmall.TransparentColor = System.Drawing.Color.Transparent;
            this.FileBrowserImageListSmall.Images.SetKeyName(0, "temp_prefab.png");
            this.FileBrowserImageListSmall.Images.SetKeyName(1, "temp_folder.png");
            this.FileBrowserImageListSmall.Images.SetKeyName(2, "temp_globe.png");
            this.FileBrowserImageListSmall.Images.SetKeyName(3, "temp_gamemanager.png");
            this.FileBrowserImageListSmall.Images.SetKeyName(4, "temp_displaymodel.png");
            // 
            // FileBrowserContextMenu
            // 
            this.FileBrowserContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.createToolStripMenuItem,
            this.toolStripSeparator1,
            this.deleteFolderToolStripMenuItem,
            this.renameToolStripMenuItem,
            this.toolStripSeparator2,
            this.viewModeToolStripMenuItem});
            this.FileBrowserContextMenu.Name = "FileBrowserContextMenu";
            this.FileBrowserContextMenu.Size = new System.Drawing.Size(129, 104);
            // 
            // createToolStripMenuItem
            // 
            this.createToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.compositeToolStripMenuItem,
            this.folderToolStripMenuItem});
            this.createToolStripMenuItem.Name = "createToolStripMenuItem";
            this.createToolStripMenuItem.Size = new System.Drawing.Size(128, 22);
            this.createToolStripMenuItem.Text = "Create";
            // 
            // compositeToolStripMenuItem
            // 
            this.compositeToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("compositeToolStripMenuItem.Image")));
            this.compositeToolStripMenuItem.Name = "compositeToolStripMenuItem";
            this.compositeToolStripMenuItem.Size = new System.Drawing.Size(132, 22);
            this.compositeToolStripMenuItem.Text = "Composite";
            this.compositeToolStripMenuItem.Click += new System.EventHandler(this.compositeToolStripMenuItem_Click);
            // 
            // folderToolStripMenuItem
            // 
            this.folderToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("folderToolStripMenuItem.Image")));
            this.folderToolStripMenuItem.Name = "folderToolStripMenuItem";
            this.folderToolStripMenuItem.Size = new System.Drawing.Size(132, 22);
            this.folderToolStripMenuItem.Text = "Folder";
            this.folderToolStripMenuItem.Click += new System.EventHandler(this.folderToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(125, 6);
            // 
            // deleteFolderToolStripMenuItem
            // 
            this.deleteFolderToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("deleteFolderToolStripMenuItem.Image")));
            this.deleteFolderToolStripMenuItem.Name = "deleteFolderToolStripMenuItem";
            this.deleteFolderToolStripMenuItem.Size = new System.Drawing.Size(128, 22);
            this.deleteFolderToolStripMenuItem.Text = "Delete";
            this.deleteFolderToolStripMenuItem.Click += new System.EventHandler(this.deleteFolderToolStripMenuItem_Click);
            // 
            // renameToolStripMenuItem
            // 
            this.renameToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("renameToolStripMenuItem.Image")));
            this.renameToolStripMenuItem.Name = "renameToolStripMenuItem";
            this.renameToolStripMenuItem.Size = new System.Drawing.Size(128, 22);
            this.renameToolStripMenuItem.Text = "Rename";
            this.renameToolStripMenuItem.Click += new System.EventHandler(this.renameToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(125, 6);
            // 
            // viewModeToolStripMenuItem
            // 
            this.viewModeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.largeIconsToolStripMenuItem,
            this.listToolStripMenuItem});
            this.viewModeToolStripMenuItem.Name = "viewModeToolStripMenuItem";
            this.viewModeToolStripMenuItem.Size = new System.Drawing.Size(128, 22);
            this.viewModeToolStripMenuItem.Text = "Display As";
            // 
            // largeIconsToolStripMenuItem
            // 
            this.largeIconsToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("largeIconsToolStripMenuItem.Image")));
            this.largeIconsToolStripMenuItem.Name = "largeIconsToolStripMenuItem";
            this.largeIconsToolStripMenuItem.Size = new System.Drawing.Size(134, 22);
            this.largeIconsToolStripMenuItem.Text = "Large Icons";
            this.largeIconsToolStripMenuItem.Click += new System.EventHandler(this.largeIconsToolStripMenuItem_Click_1);
            // 
            // listToolStripMenuItem
            // 
            this.listToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("listToolStripMenuItem.Image")));
            this.listToolStripMenuItem.Name = "listToolStripMenuItem";
            this.listToolStripMenuItem.Size = new System.Drawing.Size(134, 22);
            this.listToolStripMenuItem.Text = "List";
            this.listToolStripMenuItem.Click += new System.EventHandler(this.listToolStripMenuItem_Click);
            // 
            // treeView1
            // 
            this.treeView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.treeView1.ContextMenuStrip = this.FileTreeContextMenu;
            this.treeView1.FullRowSelect = true;
            this.treeView1.HideSelection = false;
            this.treeView1.ImageIndex = 0;
            this.treeView1.ImageList = this.imageList;
            this.treeView1.Location = new System.Drawing.Point(0, 19);
            this.treeView1.Name = "treeView1";
            this.treeView1.SelectedImageIndex = 0;
            this.treeView1.Size = new System.Drawing.Size(324, 687);
            this.treeView1.TabIndex = 180;
            this.treeView1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeView1_AfterSelect);
            this.treeView1.MouseDown += new System.Windows.Forms.MouseEventHandler(this.FileTree_MouseDown);
            this.treeView1.Resize += new System.EventHandler(this.treeView1_Resize);
            // 
            // FileTreeContextMenu
            // 
            this.FileTreeContextMenu.Name = "FileTreeContextMenu";
            this.FileTreeContextMenu.Size = new System.Drawing.Size(61, 4);
            // 
            // pathDisplay
            // 
            this.pathDisplay.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pathDisplay.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pathDisplay.Enabled = false;
            this.pathDisplay.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.pathDisplay.Location = new System.Drawing.Point(62, 0);
            this.pathDisplay.Name = "pathDisplay";
            this.pathDisplay.ReadOnly = true;
            this.pathDisplay.Size = new System.Drawing.Size(1113, 20);
            this.pathDisplay.TabIndex = 181;
            // 
            // goBackOnPath
            // 
            this.goBackOnPath.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.goBackOnPath.Location = new System.Drawing.Point(0, 0);
            this.goBackOnPath.Name = "goBackOnPath";
            this.goBackOnPath.Size = new System.Drawing.Size(63, 20);
            this.goBackOnPath.TabIndex = 182;
            this.goBackOnPath.Text = "< Back";
            this.goBackOnPath.UseVisualStyleBackColor = true;
            this.goBackOnPath.Click += new System.EventHandler(this.goBackOnPath_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.treeView1);
            this.splitContainer1.Panel1.Controls.Add(this.entity_search_box);
            this.splitContainer1.Panel1.Controls.Add(this.entity_search_btn);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.goBackOnPath);
            this.splitContainer1.Panel2.Controls.Add(this.pathDisplay);
            this.splitContainer1.Panel2.Controls.Add(this.listView1);
            this.splitContainer1.Size = new System.Drawing.Size(1504, 706);
            this.splitContainer1.SplitterDistance = 324;
            this.splitContainer1.TabIndex = 183;
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.createComposite,
            this.createFolder});
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.Top;
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(1513, 25);
            this.toolStrip1.TabIndex = 184;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // createComposite
            // 
            this.createComposite.Image = ((System.Drawing.Image)(resources.GetObject("createComposite.Image")));
            this.createComposite.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.createComposite.Name = "createComposite";
            this.createComposite.Size = new System.Drawing.Size(122, 22);
            this.createComposite.Text = "Create Composite";
            this.createComposite.Click += new System.EventHandler(this.createComposite_Click);
            // 
            // createFolder
            // 
            this.createFolder.Image = ((System.Drawing.Image)(resources.GetObject("createFolder.Image")));
            this.createFolder.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.createFolder.Name = "createFolder";
            this.createFolder.Size = new System.Drawing.Size(97, 22);
            this.createFolder.Text = "Create Folder";
            this.createFolder.Click += new System.EventHandler(this.createFolder_Click);
            // 
            // FileTreeContextMenuNew
            // 
            this.FileTreeContextMenuNew.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItem1,
            this.toolStripSeparator3,
            this.toolStripMenuItem4,
            this.toolStripMenuItem5});
            this.FileTreeContextMenuNew.Name = "FileBrowserContextMenu";
            this.FileTreeContextMenuNew.Size = new System.Drawing.Size(118, 76);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItem2,
            this.toolStripMenuItem3});
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(117, 22);
            this.toolStripMenuItem1.Text = "Create";
            // 
            // toolStripMenuItem2
            // 
            this.toolStripMenuItem2.Image = ((System.Drawing.Image)(resources.GetObject("toolStripMenuItem2.Image")));
            this.toolStripMenuItem2.Name = "toolStripMenuItem2";
            this.toolStripMenuItem2.Size = new System.Drawing.Size(159, 22);
            this.toolStripMenuItem2.Text = "New Composite";
            this.toolStripMenuItem2.Click += new System.EventHandler(this.createCompositeViaTreeView_Click);
            // 
            // toolStripMenuItem3
            // 
            this.toolStripMenuItem3.Image = ((System.Drawing.Image)(resources.GetObject("toolStripMenuItem3.Image")));
            this.toolStripMenuItem3.Name = "toolStripMenuItem3";
            this.toolStripMenuItem3.Size = new System.Drawing.Size(159, 22);
            this.toolStripMenuItem3.Text = "New Folder";
            this.toolStripMenuItem3.Click += new System.EventHandler(this.createFolderViaTreeView_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(114, 6);
            // 
            // toolStripMenuItem4
            // 
            this.toolStripMenuItem4.Image = ((System.Drawing.Image)(resources.GetObject("toolStripMenuItem4.Image")));
            this.toolStripMenuItem4.Name = "toolStripMenuItem4";
            this.toolStripMenuItem4.Size = new System.Drawing.Size(117, 22);
            this.toolStripMenuItem4.Text = "Delete";
            this.toolStripMenuItem4.Click += new System.EventHandler(this.deleteViaTreeView_Click);
            // 
            // toolStripMenuItem5
            // 
            this.toolStripMenuItem5.Image = ((System.Drawing.Image)(resources.GetObject("toolStripMenuItem5.Image")));
            this.toolStripMenuItem5.Name = "toolStripMenuItem5";
            this.toolStripMenuItem5.Size = new System.Drawing.Size(117, 22);
            this.toolStripMenuItem5.Text = "Rename";
            this.toolStripMenuItem5.Click += new System.EventHandler(this.renameViaTreeView_Click);
            // 
            // CompositeBrowser
            // 
            this.AllowEndUserDocking = false;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1513, 734);
            this.CloseButtonVisible = false;
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.toolStrip1);
            this.DockAreas = ((WeifenLuo.WinFormsUI.Docking.DockAreas)((WeifenLuo.WinFormsUI.Docking.DockAreas.Float | WeifenLuo.WinFormsUI.Docking.DockAreas.DockLeft)));
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.Name = "CompositeBrowser";
            this.ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.DockLeft;
            this.Text = "Composite Browser";
            this.FileBrowserContextMenu.ResumeLayout(false);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.FileTreeContextMenuNew.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ImageList imageList;
        private System.Windows.Forms.TextBox entity_search_box;
        private System.Windows.Forms.Button entity_search_btn;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.TreeView treeView1;
        private System.Windows.Forms.ImageList FileBrowserImageListLarge;
        private System.Windows.Forms.TextBox pathDisplay;
        private System.Windows.Forms.Button goBackOnPath;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ContextMenuStrip FileBrowserContextMenu;
        private System.Windows.Forms.ContextMenuStrip FileTreeContextMenu;
        private System.Windows.Forms.ToolStripMenuItem createToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem compositeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem folderToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem deleteFolderToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem renameToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem viewModeToolStripMenuItem;
        private ImageList FileBrowserImageListSmall;
        private ToolStripMenuItem largeIconsToolStripMenuItem;
        private ToolStripMenuItem listToolStripMenuItem;
        private ToolStrip toolStrip1;
        private ToolStripButton createFolder;
        private ToolStripButton createComposite;
        private ContextMenuStrip FileTreeContextMenuNew;
        private ToolStripMenuItem toolStripMenuItem1;
        private ToolStripMenuItem toolStripMenuItem2;
        private ToolStripMenuItem toolStripMenuItem3;
        private ToolStripSeparator toolStripSeparator3;
        private ToolStripMenuItem toolStripMenuItem4;
        private ToolStripMenuItem toolStripMenuItem5;
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2009, Daniel Kollmann
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification, are permitted
// provided that the following conditions are met:
//
// - Redistributions of source code must retain the above copyright notice, this list of conditions
//   and the following disclaimer.
//
// - Redistributions in binary form must reproduce the above copyright notice, this list of
//   conditions and the following disclaimer in the documentation and/or other materials provided
//   with the distribution.
//
// - Neither the name of Daniel Kollmann nor the names of its contributors may be used to endorse
//   or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR
// IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
// WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY
// WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Brainiac.Design
{
	partial class BehaviorTreeList
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BehaviorTreeList));
			this.treeView = new System.Windows.Forms.TreeView();
			this.imageList = new System.Windows.Forms.ImageList(this.components);
			this.toolStrip = new System.Windows.Forms.ToolStrip();
			this.fileMenuButton = new System.Windows.Forms.ToolStripDropDownButton();
			this.refreshToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.saveAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.exportAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
			this.newBehaviorButton = new System.Windows.Forms.ToolStripButton();
			this.createGroupButton = new System.Windows.Forms.ToolStripButton();
			this.deleteButton = new System.Windows.Forms.ToolStripButton();
			this.aiTypeComboBox = new System.Windows.Forms.ToolStripComboBox();
			this.helpMenuButton = new System.Windows.Forms.ToolStripDropDownButton();
			this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.reportAProblemToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.getLatestVersionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.settingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.saveFileDialog = new System.Windows.Forms.SaveFileDialog();
			this.folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
			this.toolStrip.SuspendLayout();
			this.SuspendLayout();
			// 
			// treeView
			// 
			this.treeView.AllowDrop = true;
			this.treeView.Dock = System.Windows.Forms.DockStyle.Fill;
			this.treeView.FullRowSelect = true;
			this.treeView.ImageIndex = 0;
			this.treeView.ImageList = this.imageList;
			this.treeView.LabelEdit = true;
			this.treeView.Location = new System.Drawing.Point(0, 25);
			this.treeView.Name = "treeView";
			this.treeView.SelectedImageIndex = 0;
			this.treeView.ShowNodeToolTips = true;
			this.treeView.Size = new System.Drawing.Size(401, 525);
			this.treeView.TabIndex = 1;
			this.treeView.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.behaviorTreeView_NodeMouseDoubleClick);
			this.treeView.AfterLabelEdit += new System.Windows.Forms.NodeLabelEditEventHandler(this.treeView_AfterLabelEdit);
			this.treeView.DragDrop += new System.Windows.Forms.DragEventHandler(this.treeView_DragDrop);
			this.treeView.BeforeLabelEdit += new System.Windows.Forms.NodeLabelEditEventHandler(this.treeView_BeforeLabelEdit);
			this.treeView.KeyDown += new System.Windows.Forms.KeyEventHandler(this.treeView_KeyDown);
			this.treeView.ItemDrag += new System.Windows.Forms.ItemDragEventHandler(this.treeView_ItemDrag);
			this.treeView.DragOver += new System.Windows.Forms.DragEventHandler(this.treeView_DragOver);
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
			// toolStrip
			// 
			this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileMenuButton,
            this.toolStripSeparator1,
            this.newBehaviorButton,
            this.createGroupButton,
            this.deleteButton,
            this.aiTypeComboBox,
            this.helpMenuButton});
			this.toolStrip.Location = new System.Drawing.Point(0, 0);
			this.toolStrip.Name = "toolStrip";
			this.toolStrip.Size = new System.Drawing.Size(401, 25);
			this.toolStrip.TabIndex = 0;
			this.toolStrip.Text = "toolStrip1";
			// 
			// fileMenuButton
			// 
			this.fileMenuButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.fileMenuButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.refreshToolStripMenuItem,
            this.saveAllToolStripMenuItem,
            this.exportAllToolStripMenuItem});
			this.fileMenuButton.Image = ((System.Drawing.Image)(resources.GetObject("fileMenuButton.Image")));
			this.fileMenuButton.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.fileMenuButton.Name = "fileMenuButton";
			this.fileMenuButton.Size = new System.Drawing.Size(29, 22);
			this.fileMenuButton.Text = "Behavior";
			// 
			// refreshToolStripMenuItem
			// 
			this.refreshToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("refreshToolStripMenuItem.Image")));
			this.refreshToolStripMenuItem.Name = "refreshToolStripMenuItem";
			this.refreshToolStripMenuItem.Size = new System.Drawing.Size(134, 22);
			this.refreshToolStripMenuItem.Text = "Refresh List";
			this.refreshToolStripMenuItem.Click += new System.EventHandler(this.refreshToolStripMenuItem_Click);
			// 
			// saveAllToolStripMenuItem
			// 
			this.saveAllToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("saveAllToolStripMenuItem.Image")));
			this.saveAllToolStripMenuItem.Name = "saveAllToolStripMenuItem";
			this.saveAllToolStripMenuItem.Size = new System.Drawing.Size(134, 22);
			this.saveAllToolStripMenuItem.Text = "Save All";
			this.saveAllToolStripMenuItem.Click += new System.EventHandler(this.saveAllToolStripMenuItem_Click);
			// 
			// exportAllToolStripMenuItem
			// 
			this.exportAllToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("exportAllToolStripMenuItem.Image")));
			this.exportAllToolStripMenuItem.Name = "exportAllToolStripMenuItem";
			this.exportAllToolStripMenuItem.Size = new System.Drawing.Size(134, 22);
			this.exportAllToolStripMenuItem.Text = "Export All";
			this.exportAllToolStripMenuItem.Click += new System.EventHandler(this.exportAllToolStripMenuItem_Click);
			// 
			// toolStripSeparator1
			// 
			this.toolStripSeparator1.Name = "toolStripSeparator1";
			this.toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
			// 
			// newBehaviorButton
			// 
			this.newBehaviorButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.newBehaviorButton.Image = ((System.Drawing.Image)(resources.GetObject("newBehaviorButton.Image")));
			this.newBehaviorButton.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.newBehaviorButton.Name = "newBehaviorButton";
			this.newBehaviorButton.Size = new System.Drawing.Size(23, 22);
			this.newBehaviorButton.Text = "New Behavior";
			this.newBehaviorButton.Click += new System.EventHandler(this.newBehaviorButton_Click);
			// 
			// createGroupButton
			// 
			this.createGroupButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.createGroupButton.Image = ((System.Drawing.Image)(resources.GetObject("createGroupButton.Image")));
			this.createGroupButton.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.createGroupButton.Name = "createGroupButton";
			this.createGroupButton.Size = new System.Drawing.Size(23, 22);
			this.createGroupButton.Text = "Create Group";
			this.createGroupButton.Click += new System.EventHandler(this.createGroupButton_Click);
			// 
			// deleteButton
			// 
			this.deleteButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.deleteButton.Image = ((System.Drawing.Image)(resources.GetObject("deleteButton.Image")));
			this.deleteButton.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.deleteButton.Name = "deleteButton";
			this.deleteButton.Size = new System.Drawing.Size(23, 22);
			this.deleteButton.Text = "Delete Behavior/Group";
			this.deleteButton.Click += new System.EventHandler(this.deleteButton_Click);
			// 
			// aiTypeComboBox
			// 
			this.aiTypeComboBox.AutoSize = false;
			this.aiTypeComboBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
			this.aiTypeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.aiTypeComboBox.Items.AddRange(new object[] {
            "No AI Type"});
			this.aiTypeComboBox.MaxDropDownItems = 100;
			this.aiTypeComboBox.Name = "aiTypeComboBox";
			this.aiTypeComboBox.Size = new System.Drawing.Size(90, 23);
			this.aiTypeComboBox.SelectedIndexChanged += new System.EventHandler(this.aiTypeComboBox_SelectedIndexChanged);
			// 
			// helpMenuButton
			// 
			this.helpMenuButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.helpMenuButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutToolStripMenuItem,
            this.reportAProblemToolStripMenuItem,
            this.getLatestVersionToolStripMenuItem,
            this.settingsToolStripMenuItem});
			this.helpMenuButton.Image = ((System.Drawing.Image)(resources.GetObject("helpMenuButton.Image")));
			this.helpMenuButton.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.helpMenuButton.Name = "helpMenuButton";
			this.helpMenuButton.Size = new System.Drawing.Size(29, 22);
			this.helpMenuButton.Text = "Help";
			// 
			// aboutToolStripMenuItem
			// 
			this.aboutToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("aboutToolStripMenuItem.Image")));
			this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
			this.aboutToolStripMenuItem.Size = new System.Drawing.Size(168, 22);
			this.aboutToolStripMenuItem.Text = "About";
			this.aboutToolStripMenuItem.Click += new System.EventHandler(this.aboutToolStripMenuItem_Click);
			// 
			// reportAProblemToolStripMenuItem
			// 
			this.reportAProblemToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("reportAProblemToolStripMenuItem.Image")));
			this.reportAProblemToolStripMenuItem.Name = "reportAProblemToolStripMenuItem";
			this.reportAProblemToolStripMenuItem.Size = new System.Drawing.Size(168, 22);
			this.reportAProblemToolStripMenuItem.Text = "Get Support";
			this.reportAProblemToolStripMenuItem.Click += new System.EventHandler(this.reportAProblemToolStripMenuItem_Click);
			// 
			// getLatestVersionToolStripMenuItem
			// 
			this.getLatestVersionToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("getLatestVersionToolStripMenuItem.Image")));
			this.getLatestVersionToolStripMenuItem.Name = "getLatestVersionToolStripMenuItem";
			this.getLatestVersionToolStripMenuItem.Size = new System.Drawing.Size(168, 22);
			this.getLatestVersionToolStripMenuItem.Text = "Get Latest Version";
			this.getLatestVersionToolStripMenuItem.Click += new System.EventHandler(this.getLatestVersionToolStripMenuItem_Click);
			// 
			// settingsToolStripMenuItem
			// 
			this.settingsToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("settingsToolStripMenuItem.Image")));
			this.settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
			this.settingsToolStripMenuItem.Size = new System.Drawing.Size(168, 22);
			this.settingsToolStripMenuItem.Text = "Settings";
			this.settingsToolStripMenuItem.Click += new System.EventHandler(this.settingsToolStripMenuItem_Click);
			// 
			// saveFileDialog
			// 
			this.saveFileDialog.Title = "Save Behavior";
			// 
			// folderBrowserDialog
			// 
			this.folderBrowserDialog.Description = "Open Behavior Folder";
			this.folderBrowserDialog.RootFolder = System.Environment.SpecialFolder.MyComputer;
			// 
			// BehaviorTreeList
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.treeView);
			this.Controls.Add(this.toolStrip);
			this.Name = "BehaviorTreeList";
			this.Size = new System.Drawing.Size(401, 550);
			this.toolStrip.ResumeLayout(false);
			this.toolStrip.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.TreeView treeView;
		private System.Windows.Forms.ToolStrip toolStrip;
		private System.Windows.Forms.ToolStripButton createGroupButton;
		private System.Windows.Forms.ToolStripButton deleteButton;
		private System.Windows.Forms.ToolStripButton newBehaviorButton;
		private System.Windows.Forms.ImageList imageList;
		private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
		private System.Windows.Forms.ToolStripDropDownButton fileMenuButton;
		private System.Windows.Forms.ToolStripMenuItem refreshToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem saveAllToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem exportAllToolStripMenuItem;
		private System.Windows.Forms.SaveFileDialog saveFileDialog;
		private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog;
		private System.Windows.Forms.ToolStripDropDownButton helpMenuButton;
		private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem reportAProblemToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem getLatestVersionToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem settingsToolStripMenuItem;
		private System.Windows.Forms.ToolStripComboBox aiTypeComboBox;
	}
}

namespace OpenCAGE.AnimTrees
{
    partial class AnimationTreeGraph
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AnimationTreeGraph));
            this.stNodeEditor1 = new ST.Library.UI.NodeEditor.STNodeEditor();
            this.nodeContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addNodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorAdd = new System.Windows.Forms.ToolStripSeparator();
            this.deleteNodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteLinkToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.arrangeTreeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addGhostToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.nextGhostToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteGhostToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pasteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pasteReferenceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyNodesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorCopy = new System.Windows.Forms.ToolStripSeparator();
            this.nodeContextMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // stNodeEditor1
            // 
            this.stNodeEditor1.AllowDrop = true;
            this.stNodeEditor1.AllowNodeGraphLoops = true;
            this.stNodeEditor1.AllowSameOwnerConnections = false;
            this.stNodeEditor1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(34)))), ((int)(((byte)(34)))), ((int)(((byte)(34)))));
            this.stNodeEditor1.ContextMenuStrip = this.nodeContextMenu;
            this.stNodeEditor1.Curvature = 0.3F;
            this.stNodeEditor1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.stNodeEditor1.Location = new System.Drawing.Point(0, 0);
            this.stNodeEditor1.LocationBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(120)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.stNodeEditor1.MarkBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.stNodeEditor1.MarkForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.stNodeEditor1.MinimumSize = new System.Drawing.Size(100, 100);
            this.stNodeEditor1.Name = "stNodeEditor1";
            this.stNodeEditor1.RequireCtrlForZooming = false;
            this.stNodeEditor1.RoundedCornerRadius = 10;
            this.stNodeEditor1.Size = new System.Drawing.Size(1325, 756);
            this.stNodeEditor1.TabIndex = 2;
            this.stNodeEditor1.Text = "stNodeEditor1";
            // 
            // nodeContextMenu
            // 
            this.nodeContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addNodeToolStripMenuItem,
            this.arrangeTreeToolStripMenuItem,
            this.toolStripSeparatorAdd,
            this.pasteToolStripMenuItem,
            this.pasteReferenceToolStripMenuItem,
            this.copyNodesToolStripMenuItem,
            this.toolStripSeparatorCopy,
            this.addGhostToolStripMenuItem,
            this.nextGhostToolStripMenuItem,
            this.deleteGhostToolStripMenuItem,
            this.deleteNodeToolStripMenuItem,
            this.deleteLinkToolStripMenuItem});
            this.nodeContextMenu.Name = "nodeContextMenu";
            this.nodeContextMenu.Size = new System.Drawing.Size(215, 252);
            this.nodeContextMenu.Opening += new System.ComponentModel.CancelEventHandler(this.NodeContextMenu_Opening);
            //
            // addNodeToolStripMenuItem
            //
            this.addNodeToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("addNodeToolStripMenuItem.Image")));
            this.addNodeToolStripMenuItem.Name = "addNodeToolStripMenuItem";
            this.addNodeToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.addNodeToolStripMenuItem.Text = "Add Node";
            //
            // arrangeTreeToolStripMenuItem
            //
            this.arrangeTreeToolStripMenuItem.Name = "arrangeTreeToolStripMenuItem";
            this.arrangeTreeToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.arrangeTreeToolStripMenuItem.Text = "Arrange Tree";
            this.arrangeTreeToolStripMenuItem.Click += new System.EventHandler(this.arrangeTreeToolStripMenuItem_Click);
            //
            // toolStripSeparatorAdd
            //
            this.toolStripSeparatorAdd.Name = "toolStripSeparatorAdd";
            this.toolStripSeparatorAdd.Size = new System.Drawing.Size(211, 6);
            //
            // pasteToolStripMenuItem
            //
            this.pasteToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("pasteToolStripMenuItem.Image")));
            this.pasteToolStripMenuItem.Name = "pasteToolStripMenuItem";
            this.pasteToolStripMenuItem.ShortcutKeyDisplayString = "Ctrl+V";
            this.pasteToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.pasteToolStripMenuItem.Text = "Paste";
            this.pasteToolStripMenuItem.ToolTipText = "Pastes new nodes copied from the copied ones (named to be unique in this tree), with the links that ran between them.";
            this.pasteToolStripMenuItem.Click += new System.EventHandler(this.pasteToolStripMenuItem_Click);
            //
            // pasteReferenceToolStripMenuItem
            //
            this.pasteReferenceToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("pasteReferenceToolStripMenuItem.Image")));
            this.pasteReferenceToolStripMenuItem.Name = "pasteReferenceToolStripMenuItem";
            this.pasteReferenceToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.pasteReferenceToolStripMenuItem.Text = "Paste Reference";
            this.pasteReferenceToolStripMenuItem.ToolTipText = "Pastes ghosts of the copied nodes themselves rather than new nodes: more places to draw them and their links from. Only in the tree they were copied from.";
            this.pasteReferenceToolStripMenuItem.Click += new System.EventHandler(this.pasteReferenceToolStripMenuItem_Click);
            //
            // copyNodesToolStripMenuItem
            //
            this.copyNodesToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("copyNodesToolStripMenuItem.Image")));
            this.copyNodesToolStripMenuItem.Name = "copyNodesToolStripMenuItem";
            this.copyNodesToolStripMenuItem.ShortcutKeyDisplayString = "Ctrl+C";
            this.copyNodesToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.copyNodesToolStripMenuItem.Text = "Copy";
            this.copyNodesToolStripMenuItem.Click += new System.EventHandler(this.copyNodesToolStripMenuItem_Click);
            //
            // toolStripSeparatorCopy
            //
            this.toolStripSeparatorCopy.Name = "toolStripSeparatorCopy";
            this.toolStripSeparatorCopy.Size = new System.Drawing.Size(211, 6);
            //
            // addGhostToolStripMenuItem
            //
            this.addGhostToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("addGhostToolStripMenuItem.Image")));
            this.addGhostToolStripMenuItem.Name = "addGhostToolStripMenuItem";
            this.addGhostToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.addGhostToolStripMenuItem.Text = "Add Ghost Node";
            this.addGhostToolStripMenuItem.Click += new System.EventHandler(this.addGhostToolStripMenuItem_Click);
            //
            // nextGhostToolStripMenuItem
            //
            this.nextGhostToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("nextGhostToolStripMenuItem.Image")));
            this.nextGhostToolStripMenuItem.Name = "nextGhostToolStripMenuItem";
            this.nextGhostToolStripMenuItem.ShortcutKeyDisplayString = "F3";
            this.nextGhostToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.nextGhostToolStripMenuItem.Text = "Go To Next Ghost";
            this.nextGhostToolStripMenuItem.Click += new System.EventHandler(this.nextGhostToolStripMenuItem_Click);
            //
            // deleteGhostToolStripMenuItem
            //
            this.deleteGhostToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("deleteGhostToolStripMenuItem.Image")));
            this.deleteGhostToolStripMenuItem.Name = "deleteGhostToolStripMenuItem";
            this.deleteGhostToolStripMenuItem.ShortcutKeyDisplayString = "Del";
            this.deleteGhostToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.deleteGhostToolStripMenuItem.Text = "Delete Ghost";
            this.deleteGhostToolStripMenuItem.Click += new System.EventHandler(this.deleteGhostToolStripMenuItem_Click);
            //
            // deleteNodeToolStripMenuItem
            //
            this.deleteNodeToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("deleteNodeToolStripMenuItem.Image")));
            this.deleteNodeToolStripMenuItem.Name = "deleteNodeToolStripMenuItem";
            this.deleteNodeToolStripMenuItem.ShortcutKeyDisplayString = "Shift+Del";
            this.deleteNodeToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.deleteNodeToolStripMenuItem.Text = "Delete Node";
            this.deleteNodeToolStripMenuItem.Click += new System.EventHandler(this.deleteNodeToolStripMenuItem_Click);
            // 
            // deleteLinkToolStripMenuItem
            // 
            this.deleteLinkToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("deleteLinkToolStripMenuItem.Image")));
            this.deleteLinkToolStripMenuItem.Name = "deleteLinkToolStripMenuItem";
            this.deleteLinkToolStripMenuItem.ShortcutKeyDisplayString = "Del";
            this.deleteLinkToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.deleteLinkToolStripMenuItem.Text = "Delete Link";
            this.deleteLinkToolStripMenuItem.Click += new System.EventHandler(this.deleteLinkToolStripMenuItem_Click);
            // 
            // AnimationTreeGraph
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1325, 756);
            this.Controls.Add(this.stNodeEditor1);
            this.Name = "AnimationTreeGraph";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Form1";
            this.nodeContextMenu.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private ST.Library.UI.NodeEditor.STNodeEditor stNodeEditor1;
        private System.Windows.Forms.ContextMenuStrip nodeContextMenu;
        private System.Windows.Forms.ToolStripMenuItem addNodeToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorAdd;
        private System.Windows.Forms.ToolStripMenuItem deleteNodeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteLinkToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem arrangeTreeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addGhostToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem nextGhostToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteGhostToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pasteToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pasteReferenceToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyNodesToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorCopy;
    }
}

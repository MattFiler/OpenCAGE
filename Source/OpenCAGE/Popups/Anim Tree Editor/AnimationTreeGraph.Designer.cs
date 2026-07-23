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
            this.stNodeEditor1 = new ST.Library.UI.NodeEditor.STNodeEditor();
            this.nodeContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addNodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorAdd = new System.Windows.Forms.ToolStripSeparator();
            this.deleteNodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteLinkToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.toolStripSeparatorAdd,
            this.deleteNodeToolStripMenuItem,
            this.deleteLinkToolStripMenuItem});
            this.nodeContextMenu.Name = "nodeContextMenu";
            this.nodeContextMenu.Size = new System.Drawing.Size(160, 76);
            this.nodeContextMenu.Opening += new System.ComponentModel.CancelEventHandler(this.NodeContextMenu_Opening);
            // 
            // addNodeToolStripMenuItem
            // 
            this.addNodeToolStripMenuItem.Name = "addNodeToolStripMenuItem";
            this.addNodeToolStripMenuItem.Size = new System.Drawing.Size(159, 22);
            this.addNodeToolStripMenuItem.Text = "Add Node";
            // 
            // toolStripSeparatorAdd
            // 
            this.toolStripSeparatorAdd.Name = "toolStripSeparatorAdd";
            this.toolStripSeparatorAdd.Size = new System.Drawing.Size(156, 6);
            // 
            // deleteNodeToolStripMenuItem
            // 
            this.deleteNodeToolStripMenuItem.Name = "deleteNodeToolStripMenuItem";
            this.deleteNodeToolStripMenuItem.ShortcutKeyDisplayString = "Del";
            this.deleteNodeToolStripMenuItem.Size = new System.Drawing.Size(159, 22);
            this.deleteNodeToolStripMenuItem.Text = "Delete Node";
            this.deleteNodeToolStripMenuItem.Click += new System.EventHandler(this.deleteNodeToolStripMenuItem_Click);
            // 
            // deleteLinkToolStripMenuItem
            // 
            this.deleteLinkToolStripMenuItem.Name = "deleteLinkToolStripMenuItem";
            this.deleteLinkToolStripMenuItem.ShortcutKeyDisplayString = "Del";
            this.deleteLinkToolStripMenuItem.Size = new System.Drawing.Size(159, 22);
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
    }
}

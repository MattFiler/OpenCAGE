namespace OpenCAGE
{
    partial class Flowgraph
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Flowgraph));
            this.nodeContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addAllPinsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeUnusedPinsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.managePinsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.deleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.duplicateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            this.findReferencesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.goToNextNodeInFlowgraphToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.deleteEntityToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.duplicateEntityToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createParameterToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createFunctionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createInstanceOfCompositeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createProxyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createAliasToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.addNodeForSelectedEntityToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addNodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteLinkToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.setDelayToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.clearDelayToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.stNodeEditor1 = new ST.Library.UI.NodeEditor.STNodeEditor();
            this.TabStripContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.deleteFGToolstripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.renameFGToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.createNewFlowgraphToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.nodeContextMenu.SuspendLayout();
            this.TabStripContextMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // nodeContextMenu
            // 
            this.nodeContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addAllPinsToolStripMenuItem,
            this.removeUnusedPinsToolStripMenuItem,
            this.managePinsToolStripMenuItem,
            this.toolStripSeparator1,
            this.deleteToolStripMenuItem,
            this.duplicateToolStripMenuItem,
            this.toolStripSeparator5,
            this.findReferencesToolStripMenuItem,
            this.goToNextNodeInFlowgraphToolStripMenuItem,
            this.toolStripSeparator4,
            this.deleteEntityToolStripMenuItem,
            this.duplicateEntityToolStripMenuItem,
            this.createToolStripMenuItem,
            this.addNodeForSelectedEntityToolStripMenuItem,
            this.addNodeToolStripMenuItem,
            this.deleteLinkToolStripMenuItem,
            this.setDelayToolStripMenuItem,
            this.clearDelayToolStripMenuItem});
            this.nodeContextMenu.Name = "EntityListContextMenu";
            this.nodeContextMenu.Size = new System.Drawing.Size(229, 374);
            this.nodeContextMenu.Opening += new System.ComponentModel.CancelEventHandler(this.ContextMenu_Opening);
            // 
            // addAllPinsToolStripMenuItem
            // 
            this.addAllPinsToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("addAllPinsToolStripMenuItem.Image")));
            this.addAllPinsToolStripMenuItem.Name = "addAllPinsToolStripMenuItem";
            this.addAllPinsToolStripMenuItem.ShortcutKeyDisplayString = "F4";
            this.addAllPinsToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F4;
            this.addAllPinsToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
            this.addAllPinsToolStripMenuItem.Text = "Add All Pins";
            this.addAllPinsToolStripMenuItem.Click += new System.EventHandler(this.addAllPinsToolStripMenuItem_Click);
            // 
            // removeUnusedPinsToolStripMenuItem
            // 
            this.removeUnusedPinsToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("removeUnusedPinsToolStripMenuItem.Image")));
            this.removeUnusedPinsToolStripMenuItem.Name = "removeUnusedPinsToolStripMenuItem";
            this.removeUnusedPinsToolStripMenuItem.ShortcutKeyDisplayString = "F5";
            this.removeUnusedPinsToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F5;
            this.removeUnusedPinsToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
            this.removeUnusedPinsToolStripMenuItem.Text = "Remove Unused Pins";
            this.removeUnusedPinsToolStripMenuItem.Click += new System.EventHandler(this.removeUnusedPinsToolStripMenuItem_Click);
            // 
            // managePinsToolStripMenuItem
            // 
            this.managePinsToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("managePinsToolStripMenuItem.Image")));
            this.managePinsToolStripMenuItem.Name = "managePinsToolStripMenuItem";
            this.managePinsToolStripMenuItem.ShortcutKeyDisplayString = "F6";
            this.managePinsToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F6;
            this.managePinsToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
            this.managePinsToolStripMenuItem.Text = "Manage Pins";
            this.managePinsToolStripMenuItem.Click += new System.EventHandler(this.managePinsToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(225, 6);
            // 
            // deleteToolStripMenuItem
            // 
            this.deleteToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("deleteToolStripMenuItem.Image")));
            this.deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
            this.deleteToolStripMenuItem.ShortcutKeyDisplayString = "Del";
            this.deleteToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Delete;
            this.deleteToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
            this.deleteToolStripMenuItem.Text = "Delete Node";
            this.deleteToolStripMenuItem.Click += new System.EventHandler(this.deleteToolStripMenuItem_Click);
            // 
            // duplicateToolStripMenuItem
            // 
            this.duplicateToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("duplicateToolStripMenuItem.Image")));
            this.duplicateToolStripMenuItem.Name = "duplicateToolStripMenuItem";
            this.duplicateToolStripMenuItem.ShortcutKeyDisplayString = "F1";
            this.duplicateToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F1;
            this.duplicateToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
            this.duplicateToolStripMenuItem.Text = "Duplicate Node";
            this.duplicateToolStripMenuItem.Click += new System.EventHandler(this.duplicateToolStripMenuItem_Click);
            // 
            // toolStripSeparator5
            // 
            this.toolStripSeparator5.Name = "toolStripSeparator5";
            this.toolStripSeparator5.Size = new System.Drawing.Size(225, 6);
            // 
            // findReferencesToolStripMenuItem
            // 
            this.findReferencesToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("findReferencesToolStripMenuItem.Image")));
            this.findReferencesToolStripMenuItem.Name = "findReferencesToolStripMenuItem";
            this.findReferencesToolStripMenuItem.ShortcutKeyDisplayString = "F2";
            this.findReferencesToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F2;
            this.findReferencesToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
            this.findReferencesToolStripMenuItem.Text = "Find References";
            this.findReferencesToolStripMenuItem.Click += new System.EventHandler(this.findReferencesToolStripMenuItem_Click);
            // 
            // goToNextNodeInFlowgraphToolStripMenuItem
            // 
            this.goToNextNodeInFlowgraphToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("goToNextNodeInFlowgraphToolStripMenuItem.Image")));
            this.goToNextNodeInFlowgraphToolStripMenuItem.Name = "goToNextNodeInFlowgraphToolStripMenuItem";
            this.goToNextNodeInFlowgraphToolStripMenuItem.ShortcutKeyDisplayString = "F3";
            this.goToNextNodeInFlowgraphToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F3;
            this.goToNextNodeInFlowgraphToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
            this.goToNextNodeInFlowgraphToolStripMenuItem.Text = "Go To Entity\'s Next Node";
            this.goToNextNodeInFlowgraphToolStripMenuItem.ToolTipText = "Select this to jump to the next node for this entity within the current flowgraph" +
    ".";
            this.goToNextNodeInFlowgraphToolStripMenuItem.Click += new System.EventHandler(this.goToNextNodeInFlowgraphToolStripMenuItem_Click);
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(225, 6);
            // 
            // deleteEntityToolStripMenuItem
            // 
            this.deleteEntityToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("deleteEntityToolStripMenuItem.Image")));
            this.deleteEntityToolStripMenuItem.Name = "deleteEntityToolStripMenuItem";
            this.deleteEntityToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
            this.deleteEntityToolStripMenuItem.Text = "Delete Entity";
            this.deleteEntityToolStripMenuItem.Click += new System.EventHandler(this.deleteEntityToolStripMenuItem_Click);
            // 
            // duplicateEntityToolStripMenuItem
            // 
            this.duplicateEntityToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("duplicateEntityToolStripMenuItem.Image")));
            this.duplicateEntityToolStripMenuItem.Name = "duplicateEntityToolStripMenuItem";
            this.duplicateEntityToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
            this.duplicateEntityToolStripMenuItem.Text = "Duplicate Entity";
            this.duplicateEntityToolStripMenuItem.Click += new System.EventHandler(this.duplicateEntityToolStripMenuItem_Click);
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
            this.createToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
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
            // addNodeForSelectedEntityToolStripMenuItem
            // 
            this.addNodeForSelectedEntityToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("addNodeForSelectedEntityToolStripMenuItem.Image")));
            this.addNodeForSelectedEntityToolStripMenuItem.Name = "addNodeForSelectedEntityToolStripMenuItem";
            this.addNodeForSelectedEntityToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
            this.addNodeForSelectedEntityToolStripMenuItem.Text = "Add Node For Selected Entity";
            this.addNodeForSelectedEntityToolStripMenuItem.Click += new System.EventHandler(this.addNodeForSelectedEntityToolStripMenuItem_Click);
            // 
            // addNodeToolStripMenuItem
            // 
            this.addNodeToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("addNodeToolStripMenuItem.Image")));
            this.addNodeToolStripMenuItem.Name = "addNodeToolStripMenuItem";
            this.addNodeToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
            this.addNodeToolStripMenuItem.Text = "Add Node(s)";
            this.addNodeToolStripMenuItem.Click += new System.EventHandler(this.addNodeToolStripMenuItem_Click);
            // 
            // deleteLinkToolStripMenuItem
            // 
            this.deleteLinkToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("deleteLinkToolStripMenuItem.Image")));
            this.deleteLinkToolStripMenuItem.Name = "deleteLinkToolStripMenuItem";
            this.deleteLinkToolStripMenuItem.ShortcutKeyDisplayString = "Del";
            this.deleteLinkToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Delete;
            this.deleteLinkToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
            this.deleteLinkToolStripMenuItem.Text = "Delete Link";
            this.deleteLinkToolStripMenuItem.Click += new System.EventHandler(this.deleteLinkToolStripMenuItem_Click);
            // 
            // setDelayToolStripMenuItem
            // 
            this.setDelayToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("setDelayToolStripMenuItem.Image")));
            this.setDelayToolStripMenuItem.Name = "setDelayToolStripMenuItem";
            this.setDelayToolStripMenuItem.ShortcutKeyDisplayString = "F1";
            this.setDelayToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F1;
            this.setDelayToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
            this.setDelayToolStripMenuItem.Text = "Set Delay";
            this.setDelayToolStripMenuItem.Click += new System.EventHandler(this.setDelayToolStripMenuItem_Click);
            // 
            // clearDelayToolStripMenuItem
            // 
            this.clearDelayToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("clearDelayToolStripMenuItem.Image")));
            this.clearDelayToolStripMenuItem.Name = "clearDelayToolStripMenuItem";
            this.clearDelayToolStripMenuItem.ShortcutKeyDisplayString = "F2";
            this.clearDelayToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F2;
            this.clearDelayToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
            this.clearDelayToolStripMenuItem.Text = "Clear Delay";
            this.clearDelayToolStripMenuItem.Click += new System.EventHandler(this.clearDelayToolStripMenuItem_Click);
            // 
            // stNodeEditor1
            // 
            this.stNodeEditor1.AllowDrop = false;
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
            this.stNodeEditor1.Size = new System.Drawing.Size(1512, 699);
            this.stNodeEditor1.TabIndex = 1;
            this.stNodeEditor1.Text = "stNodeEditor1";
            // 
            // TabStripContextMenu
            // 
            this.TabStripContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.deleteFGToolstripMenuItem,
            this.renameFGToolStripMenuItem,
            this.toolStripSeparator3,
            this.createNewFlowgraphToolStripMenuItem});
            this.TabStripContextMenu.Name = "TabStripContextMenu";
            this.TabStripContextMenu.Size = new System.Drawing.Size(195, 76);
            this.TabStripContextMenu.Opening += new System.ComponentModel.CancelEventHandler(this.TabStripContextMenu_Opening);
            // 
            // deleteFGToolstripMenuItem
            // 
            this.deleteFGToolstripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("deleteFGToolstripMenuItem.Image")));
            this.deleteFGToolstripMenuItem.Name = "deleteFGToolstripMenuItem";
            this.deleteFGToolstripMenuItem.Size = new System.Drawing.Size(194, 22);
            this.deleteFGToolstripMenuItem.Text = "Delete";
            this.deleteFGToolstripMenuItem.Click += new System.EventHandler(this.deleteFGToolstripMenuItem_Click);
            // 
            // renameFGToolStripMenuItem
            // 
            this.renameFGToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("renameFGToolStripMenuItem.Image")));
            this.renameFGToolStripMenuItem.Name = "renameFGToolStripMenuItem";
            this.renameFGToolStripMenuItem.Size = new System.Drawing.Size(194, 22);
            this.renameFGToolStripMenuItem.Text = "Rename ";
            this.renameFGToolStripMenuItem.Click += new System.EventHandler(this.renameFGToolStripMenuItem_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(191, 6);
            // 
            // createNewFlowgraphToolStripMenuItem
            // 
            this.createNewFlowgraphToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("createNewFlowgraphToolStripMenuItem.Image")));
            this.createNewFlowgraphToolStripMenuItem.Name = "createNewFlowgraphToolStripMenuItem";
            this.createNewFlowgraphToolStripMenuItem.Size = new System.Drawing.Size(194, 22);
            this.createNewFlowgraphToolStripMenuItem.Text = "Create New Flowgraph";
            this.createNewFlowgraphToolStripMenuItem.Click += new System.EventHandler(this.createNewFlowgraphToolStripMenuItem_Click);
            // 
            // Flowgraph
            // 
            this.AllowEndUserDocking = false;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1512, 699);
            this.CloseButton = false;
            this.CloseButtonVisible = false;
            this.Controls.Add(this.stNodeEditor1);
            this.DoubleBuffered = true;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.Name = "Flowgraph";
            this.ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.Document;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.TabPageContextMenuStrip = this.TabStripContextMenu;
            this.Text = "Flowgraph";
            this.nodeContextMenu.ResumeLayout(false);
            this.TabStripContextMenu.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private ST.Library.UI.NodeEditor.STNodeEditor stNodeEditor1;
        private System.Windows.Forms.ContextMenuStrip nodeContextMenu;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem deleteToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem duplicateToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addNodeToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip TabStripContextMenu;
        private System.Windows.Forms.ToolStripMenuItem deleteFGToolstripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem renameFGToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem createNewFlowgraphToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addAllPinsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeUnusedPinsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem managePinsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createParameterToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createFunctionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createInstanceOfCompositeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createProxyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createAliasToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem deleteEntityToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripMenuItem addNodeForSelectedEntityToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteLinkToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem duplicateEntityToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem goToNextNodeInFlowgraphToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem findReferencesToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
        private System.Windows.Forms.ToolStripMenuItem setDelayToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem clearDelayToolStripMenuItem;
    }
}



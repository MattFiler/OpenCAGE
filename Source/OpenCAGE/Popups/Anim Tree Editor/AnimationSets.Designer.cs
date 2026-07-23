namespace OpenCAGE.AnimTrees
{
    partial class AnimationSets
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
            this.animSets = new System.Windows.Forms.ListView();
            this.columnSets = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.animTrees = new System.Windows.Forms.ListView();
            this.columnTrees = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.treesPanel = new System.Windows.Forms.Panel();
            this.treeSearchHost = new System.Windows.Forms.Panel();
            this.treeSearchBox = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.treesPanel.SuspendLayout();
            this.treeSearchHost.SuspendLayout();
            this.SuspendLayout();
            // 
            // animSets
            // 
            this.animSets.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnSets});
            this.animSets.Dock = System.Windows.Forms.DockStyle.Fill;
            this.animSets.FullRowSelect = true;
            this.animSets.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.animSets.HideSelection = false;
            this.animSets.Location = new System.Drawing.Point(0, 0);
            this.animSets.MultiSelect = false;
            this.animSets.Name = "animSets";
            this.animSets.Size = new System.Drawing.Size(408, 96);
            this.animSets.TabIndex = 0;
            this.animSets.UseCompatibleStateImageBehavior = false;
            this.animSets.View = System.Windows.Forms.View.Details;
            this.animSets.SelectedIndexChanged += new System.EventHandler(this.animSets_SelectedIndexChanged);
            // 
            // columnSets
            // 
            this.columnSets.Text = "Sets";
            this.columnSets.Width = 380;
            // 
            // animTrees
            // 
            this.animTrees.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnTrees});
            this.animTrees.Dock = System.Windows.Forms.DockStyle.Fill;
            this.animTrees.FullRowSelect = true;
            this.animTrees.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.animTrees.HideSelection = false;
            this.animTrees.Location = new System.Drawing.Point(0, 26);
            this.animTrees.MultiSelect = false;
            this.animTrees.Name = "animTrees";
            this.animTrees.Size = new System.Drawing.Size(408, 331);
            this.animTrees.TabIndex = 1;
            this.animTrees.UseCompatibleStateImageBehavior = false;
            this.animTrees.View = System.Windows.Forms.View.Details;
            this.animTrees.SelectedIndexChanged += new System.EventHandler(this.animTrees_SelectedIndexChanged);
            // 
            // columnTrees
            // 
            this.columnTrees.Text = "Trees";
            this.columnTrees.Width = 380;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(12, 12);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.animSets);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.treesPanel);
            this.splitContainer1.Size = new System.Drawing.Size(408, 457);
            this.splitContainer1.SplitterDistance = 96;
            this.splitContainer1.TabIndex = 3;
            // 
            // treesPanel
            // 
            this.treesPanel.Controls.Add(this.animTrees);
            this.treesPanel.Controls.Add(this.treeSearchHost);
            this.treesPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treesPanel.Location = new System.Drawing.Point(0, 0);
            this.treesPanel.Name = "treesPanel";
            this.treesPanel.Size = new System.Drawing.Size(408, 357);
            this.treesPanel.TabIndex = 0;
            // 
            // treeSearchHost
            // 
            this.treeSearchHost.Controls.Add(this.treeSearchBox);
            this.treeSearchHost.Dock = System.Windows.Forms.DockStyle.Top;
            this.treeSearchHost.Location = new System.Drawing.Point(0, 0);
            this.treeSearchHost.Name = "treeSearchHost";
            this.treeSearchHost.Padding = new System.Windows.Forms.Padding(0, 0, 0, 6);
            this.treeSearchHost.Size = new System.Drawing.Size(408, 26);
            this.treeSearchHost.TabIndex = 0;
            // 
            // treeSearchBox
            // 
            this.treeSearchBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeSearchBox.Location = new System.Drawing.Point(0, 0);
            this.treeSearchBox.Name = "treeSearchBox";
            this.treeSearchBox.Size = new System.Drawing.Size(408, 20);
            this.treeSearchBox.TabIndex = 0;
            this.treeSearchBox.TextChanged += new System.EventHandler(this.treeSearchBox_TextChanged);
            // 
            // AnimationSets
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(432, 481);
            this.Controls.Add(this.splitContainer1);
            this.Name = "AnimationSets";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Animation Sets";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.treesPanel.ResumeLayout(false);
            this.treeSearchHost.ResumeLayout(false);
            this.treeSearchHost.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView animSets;
        private System.Windows.Forms.ColumnHeader columnSets;
        private System.Windows.Forms.ListView animTrees;
        private System.Windows.Forms.ColumnHeader columnTrees;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Panel treesPanel;
        private System.Windows.Forms.Panel treeSearchHost;
        private System.Windows.Forms.TextBox treeSearchBox;
    }
}

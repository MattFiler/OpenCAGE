namespace OpenCAGE
{
    partial class EditAnimations
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.setPanel = new System.Windows.Forms.Panel();
            this.setTree = new System.Windows.Forms.TreeView();
            this.setSearchPanel = new System.Windows.Forms.Panel();
            this.setSearchBox = new System.Windows.Forms.TextBox();
            this.setSearchLabel = new System.Windows.Forms.Label();
            this.splitClips = new System.Windows.Forms.SplitContainer();
            this.clipPanel = new System.Windows.Forms.Panel();
            this.clipList = new System.Windows.Forms.ListView();
            this.clipSearchPanel = new System.Windows.Forms.Panel();
            this.clipSearchBox = new System.Windows.Forms.TextBox();
            this.clipSearchLabel = new System.Windows.Forms.Label();
            this.clipButtonPanel = new System.Windows.Forms.Panel();
            this.previewBtn = new System.Windows.Forms.Button();
            this.exportAllBtn = new System.Windows.Forms.Button();
            this.contextLabel = new System.Windows.Forms.Label();
            this.pickBtn = new System.Windows.Forms.Button();
            this.detailBox = new System.Windows.Forms.TextBox();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            this.setPanel.SuspendLayout();
            this.setSearchPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitClips)).BeginInit();
            this.splitClips.Panel1.SuspendLayout();
            this.splitClips.Panel2.SuspendLayout();
            this.splitClips.SuspendLayout();
            this.clipPanel.SuspendLayout();
            this.clipSearchPanel.SuspendLayout();
            this.clipButtonPanel.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            //
            // splitMain
            //
            this.splitMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.Location = new System.Drawing.Point(0, 0);
            this.splitMain.Name = "splitMain";
            this.splitMain.Panel1.Controls.Add(this.setPanel);
            this.splitMain.Panel2.Controls.Add(this.splitClips);
            this.splitMain.Size = new System.Drawing.Size(1000, 618);
            this.splitMain.SplitterDistance = 330;
            this.splitMain.TabIndex = 0;
            //
            // setPanel
            //
            this.setPanel.Controls.Add(this.setTree);
            this.setPanel.Controls.Add(this.setSearchPanel);
            this.setPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.setPanel.Location = new System.Drawing.Point(0, 0);
            this.setPanel.Name = "setPanel";
            this.setPanel.Size = new System.Drawing.Size(330, 618);
            this.setPanel.TabIndex = 0;
            //
            // setTree
            //
            this.setTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.setTree.HideSelection = false;
            this.setTree.Location = new System.Drawing.Point(0, 30);
            this.setTree.Name = "setTree";
            this.setTree.Size = new System.Drawing.Size(330, 588);
            this.setTree.TabIndex = 1;
            //
            // setSearchPanel
            //
            this.setSearchPanel.Controls.Add(this.setSearchBox);
            this.setSearchPanel.Controls.Add(this.setSearchLabel);
            this.setSearchPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.setSearchPanel.Location = new System.Drawing.Point(0, 0);
            this.setSearchPanel.Name = "setSearchPanel";
            this.setSearchPanel.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.setSearchPanel.Size = new System.Drawing.Size(330, 30);
            this.setSearchPanel.TabIndex = 0;
            //
            // setSearchLabel
            //
            this.setSearchLabel.AutoSize = true;
            this.setSearchLabel.Location = new System.Drawing.Point(5, 7);
            this.setSearchLabel.Name = "setSearchLabel";
            this.setSearchLabel.Size = new System.Drawing.Size(30, 13);
            this.setSearchLabel.TabIndex = 0;
            this.setSearchLabel.Text = "Sets:";
            //
            // setSearchBox
            //
            this.setSearchBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right))));
            this.setSearchBox.Location = new System.Drawing.Point(44, 4);
            this.setSearchBox.Name = "setSearchBox";
            this.setSearchBox.Size = new System.Drawing.Size(280, 20);
            this.setSearchBox.TabIndex = 1;
            //
            // splitClips
            //
            this.splitClips.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitClips.Location = new System.Drawing.Point(0, 0);
            this.splitClips.Name = "splitClips";
            this.splitClips.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitClips.Panel1.Controls.Add(this.clipPanel);
            this.splitClips.Panel2.Controls.Add(this.detailBox);
            this.splitClips.Size = new System.Drawing.Size(666, 618);
            this.splitClips.SplitterDistance = 400;
            this.splitClips.TabIndex = 0;
            //
            // clipPanel
            //
            this.clipPanel.Controls.Add(this.clipList);
            this.clipPanel.Controls.Add(this.clipSearchPanel);
            this.clipPanel.Controls.Add(this.clipButtonPanel);
            this.clipPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.clipPanel.Location = new System.Drawing.Point(0, 0);
            this.clipPanel.Name = "clipPanel";
            this.clipPanel.Size = new System.Drawing.Size(666, 400);
            this.clipPanel.TabIndex = 0;
            //
            // clipList
            //
            this.clipList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.clipList.FullRowSelect = true;
            this.clipList.HideSelection = false;
            this.clipList.Location = new System.Drawing.Point(0, 30);
            this.clipList.Name = "clipList";
            this.clipList.Size = new System.Drawing.Size(666, 328);
            this.clipList.TabIndex = 1;
            this.clipList.UseCompatibleStateImageBehavior = false;
            this.clipList.View = System.Windows.Forms.View.Details;
            //
            // clipSearchPanel
            //
            this.clipSearchPanel.Controls.Add(this.clipSearchBox);
            this.clipSearchPanel.Controls.Add(this.clipSearchLabel);
            this.clipSearchPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.clipSearchPanel.Location = new System.Drawing.Point(0, 0);
            this.clipSearchPanel.Name = "clipSearchPanel";
            this.clipSearchPanel.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.clipSearchPanel.Size = new System.Drawing.Size(666, 30);
            this.clipSearchPanel.TabIndex = 0;
            //
            // clipSearchLabel
            //
            this.clipSearchLabel.AutoSize = true;
            this.clipSearchLabel.Location = new System.Drawing.Point(5, 7);
            this.clipSearchLabel.Name = "clipSearchLabel";
            this.clipSearchLabel.Size = new System.Drawing.Size(65, 13);
            this.clipSearchLabel.TabIndex = 0;
            this.clipSearchLabel.Text = "Animations:";
            //
            // clipSearchBox
            //
            this.clipSearchBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right))));
            this.clipSearchBox.Location = new System.Drawing.Point(76, 4);
            this.clipSearchBox.Name = "clipSearchBox";
            this.clipSearchBox.Size = new System.Drawing.Size(584, 20);
            this.clipSearchBox.TabIndex = 1;
            //
            // clipButtonPanel
            //
            this.clipButtonPanel.Controls.Add(this.contextLabel);
            this.clipButtonPanel.Controls.Add(this.pickBtn);
            this.clipButtonPanel.Controls.Add(this.previewBtn);
            this.clipButtonPanel.Controls.Add(this.exportAllBtn);
            this.clipButtonPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.clipButtonPanel.Location = new System.Drawing.Point(0, 358);
            this.clipButtonPanel.Name = "clipButtonPanel";
            this.clipButtonPanel.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.clipButtonPanel.Size = new System.Drawing.Size(666, 42);
            this.clipButtonPanel.TabIndex = 2;
            //
            // contextLabel
            //
            this.contextLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right))));
            this.contextLabel.AutoEllipsis = true;
            this.contextLabel.Location = new System.Drawing.Point(6, 11);
            this.contextLabel.Name = "contextLabel";
            this.contextLabel.Size = new System.Drawing.Size(400, 20);
            this.contextLabel.TabIndex = 0;
            this.contextLabel.Text = "Choose an animation set on the left.";
            //
            // pickBtn
            //
            this.pickBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.pickBtn.Enabled = false;
            this.pickBtn.Location = new System.Drawing.Point(410, 8);
            this.pickBtn.Name = "pickBtn";
            this.pickBtn.Size = new System.Drawing.Size(140, 26);
            this.pickBtn.TabIndex = 3;
            this.pickBtn.Text = "Use This Animation";
            this.pickBtn.UseVisualStyleBackColor = true;
            this.pickBtn.Visible = false;
            //
            // previewBtn
            //
            this.previewBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.previewBtn.Enabled = false;
            this.previewBtn.Location = new System.Drawing.Point(556, 8);
            this.previewBtn.Name = "previewBtn";
            this.previewBtn.Size = new System.Drawing.Size(104, 26);
            this.previewBtn.TabIndex = 2;
            this.previewBtn.Text = "Preview...";
            this.previewBtn.UseVisualStyleBackColor = true;
            //
            // exportAllBtn
            //
            this.exportAllBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.exportAllBtn.Enabled = false;
            this.exportAllBtn.Location = new System.Drawing.Point(442, 8);
            this.exportAllBtn.Name = "exportAllBtn";
            this.exportAllBtn.Size = new System.Drawing.Size(108, 26);
            this.exportAllBtn.TabIndex = 1;
            this.exportAllBtn.Text = "Export All...";
            this.exportAllBtn.UseVisualStyleBackColor = true;
            //
            // detailBox
            //
            this.detailBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.detailBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.detailBox.Location = new System.Drawing.Point(0, 0);
            this.detailBox.Multiline = true;
            this.detailBox.Name = "detailBox";
            this.detailBox.ReadOnly = true;
            this.detailBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.detailBox.Size = new System.Drawing.Size(666, 214);
            this.detailBox.TabIndex = 0;
            this.detailBox.WordWrap = false;
            //
            // statusStrip
            //
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 618);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(1000, 22);
            this.statusStrip.SizingGrip = false;
            this.statusStrip.TabIndex = 1;
            //
            // statusLabel
            //
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(0, 17);
            //
            // EditAnimations
            //
            this.ClientSize = new System.Drawing.Size(1000, 640);
            this.Controls.Add(this.splitMain);
            this.Controls.Add(this.statusStrip);
            this.MinimumSize = new System.Drawing.Size(760, 440);
            this.Name = "EditAnimations";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Animations";
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.setPanel.ResumeLayout(false);
            this.setSearchPanel.ResumeLayout(false);
            this.setSearchPanel.PerformLayout();
            this.splitClips.Panel1.ResumeLayout(false);
            this.splitClips.Panel2.ResumeLayout(false);
            this.splitClips.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitClips)).EndInit();
            this.splitClips.ResumeLayout(false);
            this.clipPanel.ResumeLayout(false);
            this.clipSearchPanel.ResumeLayout(false);
            this.clipSearchPanel.PerformLayout();
            this.clipButtonPanel.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.SplitContainer splitMain;
        private System.Windows.Forms.Panel setPanel;
        private System.Windows.Forms.TreeView setTree;
        private System.Windows.Forms.Panel setSearchPanel;
        private System.Windows.Forms.TextBox setSearchBox;
        private System.Windows.Forms.Label setSearchLabel;
        private System.Windows.Forms.SplitContainer splitClips;
        private System.Windows.Forms.Panel clipPanel;
        private System.Windows.Forms.ListView clipList;
        private System.Windows.Forms.Panel clipSearchPanel;
        private System.Windows.Forms.TextBox clipSearchBox;
        private System.Windows.Forms.Label clipSearchLabel;
        private System.Windows.Forms.Panel clipButtonPanel;
        private System.Windows.Forms.Button previewBtn;
        private System.Windows.Forms.Button exportAllBtn;
        private System.Windows.Forms.Label contextLabel;
        private System.Windows.Forms.Button pickBtn;
        private System.Windows.Forms.TextBox detailBox;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
    }
}

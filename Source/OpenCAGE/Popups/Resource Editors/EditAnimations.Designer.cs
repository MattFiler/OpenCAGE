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
            this.tabKinds = new System.Windows.Forms.TabControl();
            this.tabCharacters = new System.Windows.Forms.TabPage();
            this.tabEnvironment = new System.Windows.Forms.TabPage();
            this.contentPanel = new System.Windows.Forms.Panel();
            this.splitLists = new System.Windows.Forms.SplitContainer();
            this.setList = new System.Windows.Forms.ListView();
            this.setSearchPanel = new System.Windows.Forms.Panel();
            this.setSearchBox = new System.Windows.Forms.TextBox();
            this.setSearchLabel = new System.Windows.Forms.Label();
            this.clipList = new System.Windows.Forms.ListView();
            this.contextPanel = new System.Windows.Forms.Panel();
            this.contextLabel = new System.Windows.Forms.Label();
            this.contextBox = new System.Windows.Forms.ComboBox();
            this.clipSearchLabel = new System.Windows.Forms.Label();
            this.clipSearchBox = new System.Windows.Forms.TextBox();
            this.buttonPanel = new System.Windows.Forms.Panel();
            this.summaryLabel = new System.Windows.Forms.Label();
            this.pickBtn = new System.Windows.Forms.Button();
            this.previewBtn = new System.Windows.Forms.Button();
            this.exportBtn = new System.Windows.Forms.Button();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.tabKinds.SuspendLayout();
            this.contentPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitLists)).BeginInit();
            this.splitLists.Panel1.SuspendLayout();
            this.splitLists.Panel2.SuspendLayout();
            this.splitLists.SuspendLayout();
            this.setSearchPanel.SuspendLayout();
            this.contextPanel.SuspendLayout();
            this.buttonPanel.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            //
            // tabKinds
            //
            this.tabKinds.Controls.Add(this.tabCharacters);
            this.tabKinds.Controls.Add(this.tabEnvironment);
            this.tabKinds.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabKinds.Location = new System.Drawing.Point(0, 0);
            this.tabKinds.Name = "tabKinds";
            this.tabKinds.Padding = new System.Drawing.Point(12, 4);
            this.tabKinds.SelectedIndex = 0;
            this.tabKinds.Size = new System.Drawing.Size(1000, 618);
            this.tabKinds.TabIndex = 0;
            //
            // tabCharacters
            //
            this.tabCharacters.Location = new System.Drawing.Point(4, 24);
            this.tabCharacters.Name = "tabCharacters";
            this.tabCharacters.Padding = new System.Windows.Forms.Padding(3);
            this.tabCharacters.Size = new System.Drawing.Size(992, 590);
            this.tabCharacters.TabIndex = 0;
            this.tabCharacters.Text = "Character animations";
            this.tabCharacters.UseVisualStyleBackColor = true;
            //
            // tabEnvironment
            //
            this.tabEnvironment.Location = new System.Drawing.Point(4, 24);
            this.tabEnvironment.Name = "tabEnvironment";
            this.tabEnvironment.Padding = new System.Windows.Forms.Padding(3);
            this.tabEnvironment.Size = new System.Drawing.Size(992, 590);
            this.tabEnvironment.TabIndex = 1;
            this.tabEnvironment.Text = "Environment animations";
            this.tabEnvironment.UseVisualStyleBackColor = true;
            //
            // contentPanel
            //
            this.contentPanel.Controls.Add(this.splitLists);
            this.contentPanel.Controls.Add(this.buttonPanel);
            this.contentPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.contentPanel.Location = new System.Drawing.Point(3, 3);
            this.contentPanel.Name = "contentPanel";
            this.contentPanel.Size = new System.Drawing.Size(986, 584);
            this.contentPanel.TabIndex = 0;
            //
            // splitLists
            //
            this.splitLists.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitLists.Location = new System.Drawing.Point(0, 0);
            this.splitLists.Name = "splitLists";
            this.splitLists.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitLists.Panel1.Controls.Add(this.setList);
            this.splitLists.Panel1.Controls.Add(this.setSearchPanel);
            this.splitLists.Panel1MinSize = 90;
            this.splitLists.Panel2.Controls.Add(this.clipList);
            this.splitLists.Panel2.Controls.Add(this.contextPanel);
            this.splitLists.Panel2MinSize = 120;
            this.splitLists.Size = new System.Drawing.Size(986, 540);
            this.splitLists.SplitterDistance = 210;
            this.splitLists.TabIndex = 0;
            //
            // setList
            //
            this.setList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.setList.FullRowSelect = true;
            this.setList.HideSelection = false;
            this.setList.Location = new System.Drawing.Point(0, 28);
            this.setList.MultiSelect = false;
            this.setList.Name = "setList";
            this.setList.Size = new System.Drawing.Size(986, 182);
            this.setList.TabIndex = 1;
            this.setList.UseCompatibleStateImageBehavior = false;
            this.setList.View = System.Windows.Forms.View.Details;
            //
            // setSearchPanel
            //
            this.setSearchPanel.Controls.Add(this.setSearchBox);
            this.setSearchPanel.Controls.Add(this.setSearchLabel);
            this.setSearchPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.setSearchPanel.Location = new System.Drawing.Point(0, 0);
            this.setSearchPanel.Name = "setSearchPanel";
            this.setSearchPanel.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.setSearchPanel.Size = new System.Drawing.Size(986, 28);
            this.setSearchPanel.TabIndex = 0;
            //
            // setSearchLabel
            //
            this.setSearchLabel.AutoSize = true;
            this.setSearchLabel.Location = new System.Drawing.Point(4, 6);
            this.setSearchLabel.Name = "setSearchLabel";
            this.setSearchLabel.Size = new System.Drawing.Size(30, 13);
            this.setSearchLabel.TabIndex = 0;
            this.setSearchLabel.Text = "Find:";
            //
            // setSearchBox
            //
            this.setSearchBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right))));
            this.setSearchBox.Location = new System.Drawing.Point(40, 3);
            this.setSearchBox.Name = "setSearchBox";
            this.setSearchBox.Size = new System.Drawing.Size(300, 20);
            this.setSearchBox.TabIndex = 1;
            //
            // clipList
            //
            this.clipList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.clipList.FullRowSelect = true;
            this.clipList.HideSelection = false;
            this.clipList.Location = new System.Drawing.Point(0, 30);
            this.clipList.Name = "clipList";
            this.clipList.Size = new System.Drawing.Size(986, 296);
            this.clipList.TabIndex = 3;
            this.clipList.UseCompatibleStateImageBehavior = false;
            this.clipList.View = System.Windows.Forms.View.Details;
            //
            // contextPanel
            //
            this.contextPanel.Controls.Add(this.clipSearchBox);
            this.contextPanel.Controls.Add(this.clipSearchLabel);
            this.contextPanel.Controls.Add(this.contextBox);
            this.contextPanel.Controls.Add(this.contextLabel);
            this.contextPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.contextPanel.Location = new System.Drawing.Point(0, 0);
            this.contextPanel.Name = "contextPanel";
            this.contextPanel.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.contextPanel.Size = new System.Drawing.Size(986, 30);
            this.contextPanel.TabIndex = 2;
            //
            // contextLabel
            //
            this.contextLabel.AutoSize = true;
            this.contextLabel.Location = new System.Drawing.Point(4, 8);
            this.contextLabel.Name = "contextLabel";
            this.contextLabel.Size = new System.Drawing.Size(46, 13);
            this.contextLabel.TabIndex = 0;
            this.contextLabel.Text = "Context:";
            //
            // contextBox
            //
            this.contextBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.contextBox.Enabled = false;
            this.contextBox.Location = new System.Drawing.Point(56, 4);
            this.contextBox.Name = "contextBox";
            this.contextBox.Size = new System.Drawing.Size(300, 21);
            this.contextBox.TabIndex = 1;
            //
            // clipSearchLabel
            //
            this.clipSearchLabel.AutoSize = true;
            this.clipSearchLabel.Location = new System.Drawing.Point(374, 8);
            this.clipSearchLabel.Name = "clipSearchLabel";
            this.clipSearchLabel.Size = new System.Drawing.Size(30, 13);
            this.clipSearchLabel.TabIndex = 2;
            this.clipSearchLabel.Text = "Find:";
            //
            // clipSearchBox
            //
            this.clipSearchBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right))));
            this.clipSearchBox.Location = new System.Drawing.Point(410, 4);
            this.clipSearchBox.Name = "clipSearchBox";
            this.clipSearchBox.Size = new System.Drawing.Size(570, 20);
            this.clipSearchBox.TabIndex = 3;
            //
            // buttonPanel
            //
            this.buttonPanel.Controls.Add(this.summaryLabel);
            this.buttonPanel.Controls.Add(this.pickBtn);
            this.buttonPanel.Controls.Add(this.previewBtn);
            this.buttonPanel.Controls.Add(this.exportBtn);
            this.buttonPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.buttonPanel.Location = new System.Drawing.Point(0, 540);
            this.buttonPanel.Name = "buttonPanel";
            this.buttonPanel.Padding = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.buttonPanel.Size = new System.Drawing.Size(986, 44);
            this.buttonPanel.TabIndex = 1;
            //
            // summaryLabel
            //
            this.summaryLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right))));
            this.summaryLabel.AutoEllipsis = true;
            this.summaryLabel.Location = new System.Drawing.Point(6, 13);
            this.summaryLabel.Name = "summaryLabel";
            this.summaryLabel.Size = new System.Drawing.Size(560, 18);
            this.summaryLabel.TabIndex = 0;
            this.summaryLabel.Text = "Choose a skeleton above.";
            //
            // pickBtn
            //
            this.pickBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.pickBtn.Enabled = false;
            this.pickBtn.Location = new System.Drawing.Point(578, 8);
            this.pickBtn.Name = "pickBtn";
            this.pickBtn.Size = new System.Drawing.Size(150, 26);
            this.pickBtn.TabIndex = 1;
            this.pickBtn.Text = "Use This Animation";
            this.pickBtn.UseVisualStyleBackColor = true;
            this.pickBtn.Visible = false;
            //
            // previewBtn
            //
            this.previewBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.previewBtn.Enabled = false;
            this.previewBtn.Location = new System.Drawing.Point(734, 8);
            this.previewBtn.Name = "previewBtn";
            this.previewBtn.Size = new System.Drawing.Size(110, 26);
            this.previewBtn.TabIndex = 2;
            this.previewBtn.Text = "Preview...";
            this.previewBtn.UseVisualStyleBackColor = true;
            //
            // exportBtn
            //
            this.exportBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.exportBtn.Enabled = false;
            this.exportBtn.Location = new System.Drawing.Point(850, 8);
            this.exportBtn.Name = "exportBtn";
            this.exportBtn.Size = new System.Drawing.Size(130, 26);
            this.exportBtn.TabIndex = 3;
            this.exportBtn.Text = "Export All...";
            this.exportBtn.UseVisualStyleBackColor = true;
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
            this.Controls.Add(this.tabKinds);
            this.Controls.Add(this.statusStrip);
            this.MinimumSize = new System.Drawing.Size(820, 500);
            this.Name = "EditAnimations";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Animations";
            this.tabKinds.ResumeLayout(false);
            this.contentPanel.ResumeLayout(false);
            this.splitLists.Panel1.ResumeLayout(false);
            this.splitLists.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitLists)).EndInit();
            this.splitLists.ResumeLayout(false);
            this.setSearchPanel.ResumeLayout(false);
            this.setSearchPanel.PerformLayout();
            this.contextPanel.ResumeLayout(false);
            this.contextPanel.PerformLayout();
            this.buttonPanel.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TabControl tabKinds;
        private System.Windows.Forms.TabPage tabCharacters;
        private System.Windows.Forms.TabPage tabEnvironment;
        private System.Windows.Forms.Panel contentPanel;
        private System.Windows.Forms.SplitContainer splitLists;
        private System.Windows.Forms.ListView setList;
        private System.Windows.Forms.Panel setSearchPanel;
        private System.Windows.Forms.TextBox setSearchBox;
        private System.Windows.Forms.Label setSearchLabel;
        private System.Windows.Forms.ListView clipList;
        private System.Windows.Forms.Panel contextPanel;
        private System.Windows.Forms.Label contextLabel;
        private System.Windows.Forms.ComboBox contextBox;
        private System.Windows.Forms.Label clipSearchLabel;
        private System.Windows.Forms.TextBox clipSearchBox;
        private System.Windows.Forms.Panel buttonPanel;
        private System.Windows.Forms.Label summaryLabel;
        private System.Windows.Forms.Button pickBtn;
        private System.Windows.Forms.Button previewBtn;
        private System.Windows.Forms.Button exportBtn;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
    }
}

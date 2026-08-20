namespace OpenCAGE
{
    partial class EditBlendSets
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
            this.treePanel = new System.Windows.Forms.Panel();
            this.setTree = new System.Windows.Forms.TreeView();
            this.searchPanel = new System.Windows.Forms.Panel();
            this.searchBox = new System.Windows.Forms.TextBox();
            this.searchLabel = new System.Windows.Forms.Label();
            this.detailPanel = new System.Windows.Forms.Panel();
            this.splitDetail = new System.Windows.Forms.SplitContainer();
            this.spaceView = new OpenCAGE.Popups.UserControls.BlendSpaceView();
            this.tabs = new System.Windows.Forms.TabControl();
            this.tabClips = new System.Windows.Forms.TabPage();
            this.clipList = new System.Windows.Forms.ListView();
            this.clipEditPanel = new System.Windows.Forms.Panel();
            this.clipNameLabel = new System.Windows.Forms.Label();
            this.clipNameBox = new System.Windows.Forms.TextBox();
            this.pickClipBtn = new System.Windows.Forms.Button();
            this.clipDurationLabel = new System.Windows.Forms.Label();
            this.clipDurationBox = new System.Windows.Forms.TextBox();
            this.clipMirroredCheck = new System.Windows.Forms.CheckBox();
            this.tabInstances = new System.Windows.Forms.TabPage();
            this.instanceList = new System.Windows.Forms.ListView();
            this.instanceEditPanel = new System.Windows.Forms.Panel();
            this.instanceClipLabel = new System.Windows.Forms.Label();
            this.instanceClipBox = new System.Windows.Forms.ComboBox();
            this.instanceSpeedLabel = new System.Windows.Forms.Label();
            this.instanceSpeedBox = new System.Windows.Forms.TextBox();
            this.tabUsers = new System.Windows.Forms.TabPage();
            this.userList = new System.Windows.Forms.ListView();
            this.userButtonPanel = new System.Windows.Forms.Panel();
            this.addUserBtn = new System.Windows.Forms.Button();
            this.removeUserBtn = new System.Windows.Forms.Button();
            this.headerPanel = new System.Windows.Forms.Panel();
            this.headerLabel = new System.Windows.Forms.Label();
            this.noticeLabel = new System.Windows.Forms.Label();
            this.bottomPanel = new System.Windows.Forms.Panel();
            this.saveBtn = new System.Windows.Forms.Button();
            this.statusLabel = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            this.treePanel.SuspendLayout();
            this.searchPanel.SuspendLayout();
            this.detailPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitDetail)).BeginInit();
            this.splitDetail.Panel1.SuspendLayout();
            this.splitDetail.Panel2.SuspendLayout();
            this.splitDetail.SuspendLayout();
            this.tabs.SuspendLayout();
            this.tabClips.SuspendLayout();
            this.clipEditPanel.SuspendLayout();
            this.tabInstances.SuspendLayout();
            this.instanceEditPanel.SuspendLayout();
            this.tabUsers.SuspendLayout();
            this.userButtonPanel.SuspendLayout();
            this.headerPanel.SuspendLayout();
            this.bottomPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // splitMain
            //
            this.splitMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.Location = new System.Drawing.Point(0, 0);
            this.splitMain.Name = "splitMain";
            this.splitMain.Panel1.Controls.Add(this.treePanel);
            this.splitMain.Panel1MinSize = 200;
            this.splitMain.Panel2.Controls.Add(this.detailPanel);
            this.splitMain.Panel2MinSize = 420;
            this.splitMain.Size = new System.Drawing.Size(1080, 660);
            this.splitMain.SplitterDistance = 300;
            this.splitMain.TabIndex = 0;
            //
            // treePanel
            //
            this.treePanel.Controls.Add(this.setTree);
            this.treePanel.Controls.Add(this.searchPanel);
            this.treePanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treePanel.Location = new System.Drawing.Point(0, 0);
            this.treePanel.Name = "treePanel";
            this.treePanel.Size = new System.Drawing.Size(300, 660);
            this.treePanel.TabIndex = 0;
            //
            // setTree
            //
            this.setTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.setTree.HideSelection = false;
            this.setTree.Location = new System.Drawing.Point(0, 30);
            this.setTree.Name = "setTree";
            this.setTree.Size = new System.Drawing.Size(300, 630);
            this.setTree.TabIndex = 1;
            //
            // searchPanel
            //
            this.searchPanel.Controls.Add(this.searchBox);
            this.searchPanel.Controls.Add(this.searchLabel);
            this.searchPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.searchPanel.Location = new System.Drawing.Point(0, 0);
            this.searchPanel.Name = "searchPanel";
            this.searchPanel.Size = new System.Drawing.Size(300, 30);
            this.searchPanel.TabIndex = 0;
            //
            // searchBox
            //
            this.searchBox.Location = new System.Drawing.Point(52, 4);
            this.searchBox.Name = "searchBox";
            this.searchBox.Size = new System.Drawing.Size(240, 20);
            this.searchBox.TabIndex = 1;
            this.searchBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            //
            // searchLabel
            //
            this.searchLabel.AutoSize = true;
            this.searchLabel.Location = new System.Drawing.Point(6, 7);
            this.searchLabel.Name = "searchLabel";
            this.searchLabel.Size = new System.Drawing.Size(41, 13);
            this.searchLabel.TabIndex = 0;
            this.searchLabel.Text = "Search";
            //
            // detailPanel
            //
            this.detailPanel.Controls.Add(this.splitDetail);
            this.detailPanel.Controls.Add(this.headerPanel);
            this.detailPanel.Controls.Add(this.bottomPanel);
            this.detailPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.detailPanel.Location = new System.Drawing.Point(0, 0);
            this.detailPanel.Name = "detailPanel";
            this.detailPanel.Size = new System.Drawing.Size(776, 660);
            this.detailPanel.TabIndex = 0;
            //
            // splitDetail
            //
            this.splitDetail.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitDetail.Location = new System.Drawing.Point(0, 56);
            this.splitDetail.Name = "splitDetail";
            this.splitDetail.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitDetail.Panel1.Controls.Add(this.spaceView);
            this.splitDetail.Panel1MinSize = 140;
            this.splitDetail.Panel2.Controls.Add(this.tabs);
            this.splitDetail.Panel2MinSize = 180;
            this.splitDetail.Size = new System.Drawing.Size(776, 566);
            this.splitDetail.SplitterDistance = 280;
            this.splitDetail.TabIndex = 1;
            //
            // spaceView
            //
            this.spaceView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.spaceView.Location = new System.Drawing.Point(0, 0);
            this.spaceView.Name = "spaceView";
            this.spaceView.Size = new System.Drawing.Size(776, 280);
            this.spaceView.TabIndex = 0;
            //
            // tabs
            //
            this.tabs.Controls.Add(this.tabClips);
            this.tabs.Controls.Add(this.tabInstances);
            this.tabs.Controls.Add(this.tabUsers);
            this.tabs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabs.Location = new System.Drawing.Point(0, 0);
            this.tabs.Name = "tabs";
            this.tabs.SelectedIndex = 0;
            this.tabs.Size = new System.Drawing.Size(776, 282);
            this.tabs.TabIndex = 0;
            //
            // tabClips
            //
            this.tabClips.Controls.Add(this.clipList);
            this.tabClips.Controls.Add(this.clipEditPanel);
            this.tabClips.Location = new System.Drawing.Point(4, 22);
            this.tabClips.Name = "tabClips";
            this.tabClips.Padding = new System.Windows.Forms.Padding(3);
            this.tabClips.Size = new System.Drawing.Size(768, 256);
            this.tabClips.TabIndex = 0;
            this.tabClips.Text = "Clips";
            this.tabClips.UseVisualStyleBackColor = true;
            //
            // clipList
            //
            this.clipList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.clipList.FullRowSelect = true;
            this.clipList.HideSelection = false;
            this.clipList.Location = new System.Drawing.Point(3, 3);
            this.clipList.MultiSelect = false;
            this.clipList.Name = "clipList";
            this.clipList.Size = new System.Drawing.Size(762, 216);
            this.clipList.TabIndex = 0;
            this.clipList.UseCompatibleStateImageBehavior = false;
            this.clipList.View = System.Windows.Forms.View.Details;
            //
            // clipEditPanel
            //
            this.clipEditPanel.Controls.Add(this.clipNameLabel);
            this.clipEditPanel.Controls.Add(this.clipNameBox);
            this.clipEditPanel.Controls.Add(this.pickClipBtn);
            this.clipEditPanel.Controls.Add(this.clipDurationLabel);
            this.clipEditPanel.Controls.Add(this.clipDurationBox);
            this.clipEditPanel.Controls.Add(this.clipMirroredCheck);
            this.clipEditPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.clipEditPanel.Location = new System.Drawing.Point(3, 219);
            this.clipEditPanel.Name = "clipEditPanel";
            this.clipEditPanel.Size = new System.Drawing.Size(762, 34);
            this.clipEditPanel.TabIndex = 1;
            //
            // clipNameLabel
            //
            this.clipNameLabel.AutoSize = true;
            this.clipNameLabel.Location = new System.Drawing.Point(4, 10);
            this.clipNameLabel.Name = "clipNameLabel";
            this.clipNameLabel.Size = new System.Drawing.Size(27, 13);
            this.clipNameLabel.TabIndex = 0;
            this.clipNameLabel.Text = "Clip";
            //
            // clipNameBox
            //
            this.clipNameBox.Location = new System.Drawing.Point(36, 7);
            this.clipNameBox.Name = "clipNameBox";
            this.clipNameBox.Size = new System.Drawing.Size(300, 20);
            this.clipNameBox.TabIndex = 1;
            //
            // pickClipBtn
            //
            this.pickClipBtn.Location = new System.Drawing.Point(342, 5);
            this.pickClipBtn.Name = "pickClipBtn";
            this.pickClipBtn.Size = new System.Drawing.Size(90, 24);
            this.pickClipBtn.TabIndex = 2;
            this.pickClipBtn.Text = "Choose...";
            this.pickClipBtn.UseVisualStyleBackColor = true;
            //
            // clipDurationLabel
            //
            this.clipDurationLabel.AutoSize = true;
            this.clipDurationLabel.Location = new System.Drawing.Point(444, 10);
            this.clipDurationLabel.Name = "clipDurationLabel";
            this.clipDurationLabel.Size = new System.Drawing.Size(50, 13);
            this.clipDurationLabel.TabIndex = 3;
            this.clipDurationLabel.Text = "Length s";
            //
            // clipDurationBox
            //
            this.clipDurationBox.Location = new System.Drawing.Point(498, 7);
            this.clipDurationBox.Name = "clipDurationBox";
            this.clipDurationBox.Size = new System.Drawing.Size(60, 20);
            this.clipDurationBox.TabIndex = 4;
            //
            // clipMirroredCheck
            //
            this.clipMirroredCheck.AutoSize = true;
            this.clipMirroredCheck.Location = new System.Drawing.Point(572, 9);
            this.clipMirroredCheck.Name = "clipMirroredCheck";
            this.clipMirroredCheck.Size = new System.Drawing.Size(65, 17);
            this.clipMirroredCheck.TabIndex = 5;
            this.clipMirroredCheck.Text = "Mirrored";
            this.clipMirroredCheck.UseVisualStyleBackColor = true;
            //
            // tabInstances
            //
            this.tabInstances.Controls.Add(this.instanceList);
            this.tabInstances.Controls.Add(this.instanceEditPanel);
            this.tabInstances.Location = new System.Drawing.Point(4, 22);
            this.tabInstances.Name = "tabInstances";
            this.tabInstances.Padding = new System.Windows.Forms.Padding(3);
            this.tabInstances.Size = new System.Drawing.Size(768, 256);
            this.tabInstances.TabIndex = 1;
            this.tabInstances.Text = "Blend points";
            this.tabInstances.UseVisualStyleBackColor = true;
            //
            // instanceList
            //
            this.instanceList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.instanceList.FullRowSelect = true;
            this.instanceList.HideSelection = false;
            this.instanceList.Location = new System.Drawing.Point(3, 3);
            this.instanceList.MultiSelect = false;
            this.instanceList.Name = "instanceList";
            this.instanceList.Size = new System.Drawing.Size(762, 216);
            this.instanceList.TabIndex = 0;
            this.instanceList.UseCompatibleStateImageBehavior = false;
            this.instanceList.View = System.Windows.Forms.View.Details;
            //
            // instanceEditPanel
            //
            this.instanceEditPanel.Controls.Add(this.instanceClipLabel);
            this.instanceEditPanel.Controls.Add(this.instanceClipBox);
            this.instanceEditPanel.Controls.Add(this.instanceSpeedLabel);
            this.instanceEditPanel.Controls.Add(this.instanceSpeedBox);
            this.instanceEditPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.instanceEditPanel.Location = new System.Drawing.Point(3, 219);
            this.instanceEditPanel.Name = "instanceEditPanel";
            this.instanceEditPanel.Size = new System.Drawing.Size(762, 34);
            this.instanceEditPanel.TabIndex = 1;
            //
            // instanceClipLabel
            //
            this.instanceClipLabel.AutoSize = true;
            this.instanceClipLabel.Location = new System.Drawing.Point(4, 10);
            this.instanceClipLabel.Name = "instanceClipLabel";
            this.instanceClipLabel.Size = new System.Drawing.Size(50, 13);
            this.instanceClipLabel.TabIndex = 0;
            this.instanceClipLabel.Text = "Plays";
            //
            // instanceClipBox
            //
            this.instanceClipBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.instanceClipBox.Location = new System.Drawing.Point(56, 6);
            this.instanceClipBox.Name = "instanceClipBox";
            this.instanceClipBox.Size = new System.Drawing.Size(340, 21);
            this.instanceClipBox.TabIndex = 1;
            //
            // instanceSpeedLabel
            //
            this.instanceSpeedLabel.AutoSize = true;
            this.instanceSpeedLabel.Location = new System.Drawing.Point(410, 10);
            this.instanceSpeedLabel.Name = "instanceSpeedLabel";
            this.instanceSpeedLabel.Size = new System.Drawing.Size(38, 13);
            this.instanceSpeedLabel.TabIndex = 2;
            this.instanceSpeedLabel.Text = "Speed";
            //
            // instanceSpeedBox
            //
            this.instanceSpeedBox.Location = new System.Drawing.Point(452, 7);
            this.instanceSpeedBox.Name = "instanceSpeedBox";
            this.instanceSpeedBox.Size = new System.Drawing.Size(60, 20);
            this.instanceSpeedBox.TabIndex = 3;
            //
            // tabUsers
            //
            this.tabUsers.Controls.Add(this.userList);
            this.tabUsers.Controls.Add(this.userButtonPanel);
            this.tabUsers.Location = new System.Drawing.Point(4, 22);
            this.tabUsers.Name = "tabUsers";
            this.tabUsers.Padding = new System.Windows.Forms.Padding(3);
            this.tabUsers.Size = new System.Drawing.Size(768, 256);
            this.tabUsers.TabIndex = 2;
            this.tabUsers.Text = "Used by";
            this.tabUsers.UseVisualStyleBackColor = true;
            //
            // userList
            //
            this.userList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.userList.FullRowSelect = true;
            this.userList.HideSelection = false;
            this.userList.Location = new System.Drawing.Point(3, 3);
            this.userList.Name = "userList";
            this.userList.Size = new System.Drawing.Size(762, 216);
            this.userList.TabIndex = 0;
            this.userList.UseCompatibleStateImageBehavior = false;
            this.userList.View = System.Windows.Forms.View.Details;
            //
            // userButtonPanel
            //
            this.userButtonPanel.Controls.Add(this.addUserBtn);
            this.userButtonPanel.Controls.Add(this.removeUserBtn);
            this.userButtonPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.userButtonPanel.Location = new System.Drawing.Point(3, 219);
            this.userButtonPanel.Name = "userButtonPanel";
            this.userButtonPanel.Size = new System.Drawing.Size(762, 34);
            this.userButtonPanel.TabIndex = 1;
            //
            // addUserBtn
            //
            this.addUserBtn.Location = new System.Drawing.Point(4, 5);
            this.addUserBtn.Name = "addUserBtn";
            this.addUserBtn.Size = new System.Drawing.Size(140, 24);
            this.addUserBtn.TabIndex = 0;
            this.addUserBtn.Text = "Give to character...";
            this.addUserBtn.UseVisualStyleBackColor = true;
            //
            // removeUserBtn
            //
            this.removeUserBtn.Location = new System.Drawing.Point(150, 5);
            this.removeUserBtn.Name = "removeUserBtn";
            this.removeUserBtn.Size = new System.Drawing.Size(90, 24);
            this.removeUserBtn.TabIndex = 1;
            this.removeUserBtn.Text = "Take away";
            this.removeUserBtn.UseVisualStyleBackColor = true;
            //
            // headerPanel
            //
            this.headerPanel.Controls.Add(this.noticeLabel);
            this.headerPanel.Controls.Add(this.headerLabel);
            this.headerPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.headerPanel.Location = new System.Drawing.Point(0, 0);
            this.headerPanel.Name = "headerPanel";
            this.headerPanel.Size = new System.Drawing.Size(776, 56);
            this.headerPanel.TabIndex = 0;
            //
            // headerLabel
            //
            this.headerLabel.AutoSize = true;
            this.headerLabel.Location = new System.Drawing.Point(8, 8);
            this.headerLabel.Name = "headerLabel";
            this.headerLabel.Size = new System.Drawing.Size(0, 13);
            this.headerLabel.TabIndex = 0;
            //
            // noticeLabel
            //
            this.noticeLabel.Location = new System.Drawing.Point(8, 26);
            this.noticeLabel.Name = "noticeLabel";
            this.noticeLabel.Size = new System.Drawing.Size(760, 28);
            this.noticeLabel.TabIndex = 1;
            this.noticeLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            //
            // bottomPanel
            //
            this.bottomPanel.Controls.Add(this.statusLabel);
            this.bottomPanel.Controls.Add(this.saveBtn);
            this.bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottomPanel.Location = new System.Drawing.Point(0, 622);
            this.bottomPanel.Name = "bottomPanel";
            this.bottomPanel.Size = new System.Drawing.Size(776, 38);
            this.bottomPanel.TabIndex = 2;
            //
            // saveBtn
            //
            this.saveBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.saveBtn.Location = new System.Drawing.Point(678, 7);
            this.saveBtn.Name = "saveBtn";
            this.saveBtn.Size = new System.Drawing.Size(90, 25);
            this.saveBtn.TabIndex = 1;
            this.saveBtn.Text = "Save";
            this.saveBtn.UseVisualStyleBackColor = true;
            //
            // statusLabel
            //
            this.statusLabel.AutoSize = true;
            this.statusLabel.Location = new System.Drawing.Point(8, 13);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(0, 13);
            this.statusLabel.TabIndex = 0;
            //
            // EditBlendSets
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1080, 660);
            this.Controls.Add(this.splitMain);
            this.MinimumSize = new System.Drawing.Size(820, 520);
            this.Name = "EditBlendSets";
            this.Text = "Blend Sets";
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.treePanel.ResumeLayout(false);
            this.searchPanel.ResumeLayout(false);
            this.searchPanel.PerformLayout();
            this.detailPanel.ResumeLayout(false);
            this.splitDetail.Panel1.ResumeLayout(false);
            this.splitDetail.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitDetail)).EndInit();
            this.splitDetail.ResumeLayout(false);
            this.tabs.ResumeLayout(false);
            this.tabClips.ResumeLayout(false);
            this.clipEditPanel.ResumeLayout(false);
            this.clipEditPanel.PerformLayout();
            this.tabInstances.ResumeLayout(false);
            this.instanceEditPanel.ResumeLayout(false);
            this.instanceEditPanel.PerformLayout();
            this.tabUsers.ResumeLayout(false);
            this.userButtonPanel.ResumeLayout(false);
            this.headerPanel.ResumeLayout(false);
            this.headerPanel.PerformLayout();
            this.bottomPanel.ResumeLayout(false);
            this.bottomPanel.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.SplitContainer splitMain;
        private System.Windows.Forms.Panel treePanel;
        private System.Windows.Forms.TreeView setTree;
        private System.Windows.Forms.Panel searchPanel;
        private System.Windows.Forms.TextBox searchBox;
        private System.Windows.Forms.Label searchLabel;
        private System.Windows.Forms.Panel detailPanel;
        private System.Windows.Forms.SplitContainer splitDetail;
        private OpenCAGE.Popups.UserControls.BlendSpaceView spaceView;
        private System.Windows.Forms.TabControl tabs;
        private System.Windows.Forms.TabPage tabClips;
        private System.Windows.Forms.ListView clipList;
        private System.Windows.Forms.Panel clipEditPanel;
        private System.Windows.Forms.Label clipNameLabel;
        private System.Windows.Forms.TextBox clipNameBox;
        private System.Windows.Forms.Button pickClipBtn;
        private System.Windows.Forms.Label clipDurationLabel;
        private System.Windows.Forms.TextBox clipDurationBox;
        private System.Windows.Forms.CheckBox clipMirroredCheck;
        private System.Windows.Forms.TabPage tabInstances;
        private System.Windows.Forms.ListView instanceList;
        private System.Windows.Forms.Panel instanceEditPanel;
        private System.Windows.Forms.Label instanceClipLabel;
        private System.Windows.Forms.ComboBox instanceClipBox;
        private System.Windows.Forms.Label instanceSpeedLabel;
        private System.Windows.Forms.TextBox instanceSpeedBox;
        private System.Windows.Forms.TabPage tabUsers;
        private System.Windows.Forms.ListView userList;
        private System.Windows.Forms.Panel userButtonPanel;
        private System.Windows.Forms.Button addUserBtn;
        private System.Windows.Forms.Button removeUserBtn;
        private System.Windows.Forms.Panel headerPanel;
        private System.Windows.Forms.Label headerLabel;
        private System.Windows.Forms.Label noticeLabel;
        private System.Windows.Forms.Panel bottomPanel;
        private System.Windows.Forms.Button saveBtn;
        private System.Windows.Forms.Label statusLabel;
    }
}

namespace OpenCAGE
{
    partial class AnimationPreview
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
            this.topPanel = new System.Windows.Forms.Panel();
            this.exportBtn = new System.Windows.Forms.Button();
            this.modelBtn = new System.Windows.Forms.Button();
            this.modelLabel = new System.Windows.Forms.Label();
            this.skeletonBtn = new System.Windows.Forms.Button();
            this.skeletonLabel = new System.Windows.Forms.Label();
            this.clipLabel = new System.Windows.Forms.Label();
            this.warningLabel = new System.Windows.Forms.Label();
            this.splitViewer = new System.Windows.Forms.SplitContainer();
            this.viewerHost = new System.Windows.Forms.Integration.ElementHost();
            this.timelinePanel = new System.Windows.Forms.Panel();
            this.timeline = new OpenCAGE.Popups.UserControls.AnimationTimeline();
            this.markerLabel = new System.Windows.Forms.Label();
            this.transportPanel = new System.Windows.Forms.Panel();
            this.playBtn = new System.Windows.Forms.Button();
            this.frameLabel = new System.Windows.Forms.Label();
            this.speedLabel = new System.Windows.Forms.Label();
            this.speedBox = new System.Windows.Forms.ComboBox();
            this.loopCheck = new System.Windows.Forms.CheckBox();
            this.bonesCheck = new System.Windows.Forms.CheckBox();
            this.showMeshCheck = new System.Windows.Forms.CheckBox();
            this.meshCheck = new System.Windows.Forms.CheckBox();
            this.markerCountLabel = new System.Windows.Forms.Label();
            this.rootMotionCheck = new System.Windows.Forms.CheckBox();
            this.partGroup = new System.Windows.Forms.GroupBox();
            this.partPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.partButtons = new System.Windows.Forms.Panel();
            this.showAllPartsBtn = new System.Windows.Forms.Button();
            this.hideAllPartsBtn = new System.Windows.Forms.Button();
            this.partGroup.SuspendLayout();
            this.partButtons.SuspendLayout();
            this.topPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitViewer)).BeginInit();
            this.splitViewer.Panel1.SuspendLayout();
            this.splitViewer.Panel2.SuspendLayout();
            this.splitViewer.SuspendLayout();
            this.timelinePanel.SuspendLayout();
            this.transportPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // topPanel
            //
            this.topPanel.Controls.Add(this.clipLabel);
            this.topPanel.Controls.Add(this.modelLabel);
            this.topPanel.Controls.Add(this.modelBtn);
            this.topPanel.Controls.Add(this.skeletonLabel);
            this.topPanel.Controls.Add(this.skeletonBtn);
            this.topPanel.Controls.Add(this.exportBtn);
            this.topPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.topPanel.Location = new System.Drawing.Point(0, 0);
            this.topPanel.Name = "topPanel";
            this.topPanel.Size = new System.Drawing.Size(980, 82);
            this.topPanel.TabIndex = 0;
            //
            // clipLabel
            //
            this.clipLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right))));
            this.clipLabel.AutoEllipsis = true;
            this.clipLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold);
            this.clipLabel.Location = new System.Drawing.Point(10, 8);
            this.clipLabel.Name = "clipLabel";
            this.clipLabel.Size = new System.Drawing.Size(830, 18);
            this.clipLabel.TabIndex = 0;
            this.clipLabel.Text = "No animation selected";
            //
            // modelBtn
            //
            this.modelBtn.Location = new System.Drawing.Point(10, 31);
            this.modelBtn.Name = "modelBtn";
            this.modelBtn.Size = new System.Drawing.Size(106, 24);
            this.modelBtn.TabIndex = 1;
            this.modelBtn.Text = "Choose Mesh...";
            this.modelBtn.UseVisualStyleBackColor = true;
            //
            // modelLabel
            //
            this.modelLabel.AutoEllipsis = true;
            this.modelLabel.Location = new System.Drawing.Point(122, 34);
            this.modelLabel.Name = "modelLabel";
            this.modelLabel.Size = new System.Drawing.Size(320, 20);
            this.modelLabel.TabIndex = 2;
            this.modelLabel.Text = "None";
            this.modelLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // skeletonBtn
            //
            this.skeletonBtn.Location = new System.Drawing.Point(10, 55);
            this.skeletonBtn.Name = "skeletonBtn";
            this.skeletonBtn.Size = new System.Drawing.Size(106, 24);
            this.skeletonBtn.TabIndex = 3;
            this.skeletonBtn.Text = "Choose Rig...";
            this.skeletonBtn.UseVisualStyleBackColor = true;
            //
            // skeletonLabel
            //
            this.skeletonLabel.AutoEllipsis = true;
            this.skeletonLabel.Location = new System.Drawing.Point(122, 57);
            this.skeletonLabel.Name = "skeletonLabel";
            this.skeletonLabel.Size = new System.Drawing.Size(320, 20);
            this.skeletonLabel.TabIndex = 4;
            this.skeletonLabel.Text = "None";
            this.skeletonLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // exportBtn
            //
            this.exportBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.exportBtn.Enabled = false;
            this.exportBtn.Location = new System.Drawing.Point(856, 42);
            this.exportBtn.Name = "exportBtn";
            this.exportBtn.Size = new System.Drawing.Size(112, 28);
            this.exportBtn.TabIndex = 5;
            this.exportBtn.Text = "Export...";
            this.exportBtn.UseVisualStyleBackColor = true;
            //
            // warningLabel
            //
            this.warningLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this.warningLabel.Location = new System.Drawing.Point(0, 82);
            this.warningLabel.Name = "warningLabel";
            this.warningLabel.Padding = new System.Windows.Forms.Padding(10, 4, 10, 4);
            this.warningLabel.Size = new System.Drawing.Size(980, 32);
            this.warningLabel.TabIndex = 1;
            this.warningLabel.Visible = false;
            //
            // splitViewer
            //
            this.splitViewer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitViewer.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitViewer.Location = new System.Drawing.Point(0, 114);
            this.splitViewer.Name = "splitViewer";
            this.splitViewer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitViewer.Panel1.Controls.Add(this.viewerHost);
            this.splitViewer.Panel1.Controls.Add(this.partGroup);
            this.splitViewer.Panel1MinSize = 140;
            this.splitViewer.Panel2.Controls.Add(this.timelinePanel);
            this.splitViewer.Panel2MinSize = 80;
            this.splitViewer.Size = new System.Drawing.Size(980, 526);
            this.splitViewer.SplitterDistance = 340;
            this.splitViewer.TabIndex = 2;
            //
            // viewerHost
            //
            this.viewerHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.viewerHost.Location = new System.Drawing.Point(0, 0);
            this.viewerHost.Name = "viewerHost";
            this.viewerHost.Size = new System.Drawing.Size(980, 340);
            this.viewerHost.TabIndex = 0;
            this.viewerHost.Child = null;
            //
            // partGroup
            //
            this.partGroup.Controls.Add(this.partPanel);
            this.partGroup.Controls.Add(this.partButtons);
            this.partGroup.Dock = System.Windows.Forms.DockStyle.Right;
            this.partGroup.Location = new System.Drawing.Point(722, 0);
            this.partGroup.Name = "partGroup";
            this.partGroup.Padding = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.partGroup.Size = new System.Drawing.Size(258, 340);
            this.partGroup.TabIndex = 1;
            this.partGroup.TabStop = false;
            this.partGroup.Text = "Parts of the mesh";
            //
            // partPanel
            //
            this.partPanel.AutoScroll = true;
            this.partPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.partPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.partPanel.Location = new System.Drawing.Point(6, 17);
            this.partPanel.Name = "partPanel";
            this.partPanel.Size = new System.Drawing.Size(208, 289);
            this.partPanel.TabIndex = 0;
            this.partPanel.WrapContents = false;
            //
            // partButtons
            //
            this.partButtons.Controls.Add(this.showAllPartsBtn);
            this.partButtons.Controls.Add(this.hideAllPartsBtn);
            this.partButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.partButtons.Location = new System.Drawing.Point(6, 306);
            this.partButtons.Name = "partButtons";
            this.partButtons.Size = new System.Drawing.Size(208, 30);
            this.partButtons.TabIndex = 1;
            //
            // showAllPartsBtn
            //
            this.showAllPartsBtn.Location = new System.Drawing.Point(0, 3);
            this.showAllPartsBtn.Name = "showAllPartsBtn";
            this.showAllPartsBtn.Size = new System.Drawing.Size(90, 24);
            this.showAllPartsBtn.TabIndex = 0;
            this.showAllPartsBtn.Text = "Show all";
            this.showAllPartsBtn.UseVisualStyleBackColor = true;
            //
            // hideAllPartsBtn
            //
            this.hideAllPartsBtn.Location = new System.Drawing.Point(96, 3);
            this.hideAllPartsBtn.Name = "hideAllPartsBtn";
            this.hideAllPartsBtn.Size = new System.Drawing.Size(90, 24);
            this.hideAllPartsBtn.TabIndex = 1;
            this.hideAllPartsBtn.Text = "Hide all";
            this.hideAllPartsBtn.UseVisualStyleBackColor = true;
            //
            // timelinePanel
            //
            this.timelinePanel.Controls.Add(this.timeline);
            this.timelinePanel.Controls.Add(this.markerLabel);
            this.timelinePanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.timelinePanel.Location = new System.Drawing.Point(0, 0);
            this.timelinePanel.Name = "timelinePanel";
            this.timelinePanel.Size = new System.Drawing.Size(980, 182);
            this.timelinePanel.TabIndex = 0;
            //
            // timeline
            //
            this.timeline.Dock = System.Windows.Forms.DockStyle.Fill;
            this.timeline.Location = new System.Drawing.Point(0, 0);
            this.timeline.Name = "timeline";
            this.timeline.Size = new System.Drawing.Size(980, 148);
            this.timeline.TabIndex = 0;
            //
            // markerLabel
            //
            this.markerLabel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.markerLabel.Location = new System.Drawing.Point(0, 148);
            this.markerLabel.Name = "markerLabel";
            this.markerLabel.Padding = new System.Windows.Forms.Padding(8, 6, 8, 6);
            this.markerLabel.Size = new System.Drawing.Size(980, 34);
            this.markerLabel.TabIndex = 1;
            this.markerLabel.Text = "Click a marker to see what it does.";
            //
            // transportPanel
            //
            this.transportPanel.Controls.Add(this.markerCountLabel);
            this.transportPanel.Controls.Add(this.rootMotionCheck);
            this.transportPanel.Controls.Add(this.playBtn);
            this.transportPanel.Controls.Add(this.frameLabel);
            this.transportPanel.Controls.Add(this.speedLabel);
            this.transportPanel.Controls.Add(this.speedBox);
            this.transportPanel.Controls.Add(this.loopCheck);
            this.transportPanel.Controls.Add(this.bonesCheck);
            this.transportPanel.Controls.Add(this.showMeshCheck);
            this.transportPanel.Controls.Add(this.meshCheck);
            this.transportPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.transportPanel.Location = new System.Drawing.Point(0, 640);
            this.transportPanel.Name = "transportPanel";
            this.transportPanel.Size = new System.Drawing.Size(980, 44);
            this.transportPanel.TabIndex = 3;
            //
            // playBtn
            //
            this.playBtn.Location = new System.Drawing.Point(10, 9);
            this.playBtn.Name = "playBtn";
            this.playBtn.Size = new System.Drawing.Size(70, 26);
            this.playBtn.TabIndex = 0;
            this.playBtn.Text = "Play";
            this.playBtn.UseVisualStyleBackColor = true;
            //
            // frameLabel
            //
            this.frameLabel.Location = new System.Drawing.Point(88, 14);
            this.frameLabel.Name = "frameLabel";
            this.frameLabel.Size = new System.Drawing.Size(190, 20);
            this.frameLabel.TabIndex = 1;
            this.frameLabel.Text = "-";
            //
            // speedLabel
            //
            this.speedLabel.AutoSize = true;
            this.speedLabel.Location = new System.Drawing.Point(284, 15);
            this.speedLabel.Name = "speedLabel";
            this.speedLabel.Size = new System.Drawing.Size(41, 13);
            this.speedLabel.TabIndex = 2;
            this.speedLabel.Text = "Speed:";
            //
            // speedBox
            //
            this.speedBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.speedBox.Location = new System.Drawing.Point(330, 11);
            this.speedBox.Name = "speedBox";
            this.speedBox.Size = new System.Drawing.Size(72, 21);
            this.speedBox.TabIndex = 3;
            //
            // loopCheck
            //
            this.loopCheck.AutoSize = true;
            this.loopCheck.Checked = true;
            this.loopCheck.Location = new System.Drawing.Point(418, 14);
            this.loopCheck.Name = "loopCheck";
            this.loopCheck.Size = new System.Drawing.Size(50, 17);
            this.loopCheck.TabIndex = 4;
            this.loopCheck.Text = "Loop";
            this.loopCheck.UseVisualStyleBackColor = true;
            //
            // bonesCheck
            //
            this.bonesCheck.AutoSize = true;
            this.bonesCheck.Checked = true;
            this.bonesCheck.Location = new System.Drawing.Point(474, 14);
            this.bonesCheck.Name = "bonesCheck";
            this.bonesCheck.Size = new System.Drawing.Size(70, 17);
            this.bonesCheck.TabIndex = 5;
            this.bonesCheck.Text = "Show rig";
            this.bonesCheck.UseVisualStyleBackColor = true;
            //
            // showMeshCheck
            //
            this.showMeshCheck.AutoSize = true;
            this.showMeshCheck.Checked = true;
            this.showMeshCheck.Location = new System.Drawing.Point(550, 14);
            this.showMeshCheck.Name = "showMeshCheck";
            this.showMeshCheck.Size = new System.Drawing.Size(82, 17);
            this.showMeshCheck.TabIndex = 6;
            this.showMeshCheck.Text = "Show mesh";
            this.showMeshCheck.UseVisualStyleBackColor = true;
            //
            // meshCheck
            //
            this.meshCheck.AutoSize = true;
            this.meshCheck.Checked = true;
            this.meshCheck.Location = new System.Drawing.Point(636, 14);
            this.meshCheck.Name = "meshCheck";
            this.meshCheck.Size = new System.Drawing.Size(94, 17);
            this.meshCheck.TabIndex = 7;
            this.meshCheck.Text = "Show textures";
            this.meshCheck.UseVisualStyleBackColor = true;
            //
            // rootMotionCheck
            //
            this.rootMotionCheck.AutoSize = true;
            this.rootMotionCheck.Location = new System.Drawing.Point(736, 14);
            this.rootMotionCheck.Name = "rootMotionCheck";
            this.rootMotionCheck.Size = new System.Drawing.Size(84, 17);
            this.rootMotionCheck.TabIndex = 8;
            this.rootMotionCheck.Text = "Root motion";
            this.rootMotionCheck.UseVisualStyleBackColor = true;
            //
            // markerCountLabel
            //
            this.markerCountLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.markerCountLabel.Location = new System.Drawing.Point(832, 14);
            this.markerCountLabel.Name = "markerCountLabel";
            this.markerCountLabel.Size = new System.Drawing.Size(136, 20);
            this.markerCountLabel.TabIndex = 9;
            this.markerCountLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // AnimationPreview
            //
            this.ClientSize = new System.Drawing.Size(980, 684);
            this.Controls.Add(this.splitViewer);
            this.Controls.Add(this.warningLabel);
            this.Controls.Add(this.topPanel);
            this.Controls.Add(this.transportPanel);
            this.MinimumSize = new System.Drawing.Size(760, 520);
            this.Name = "AnimationPreview";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Animation Preview";
            this.topPanel.ResumeLayout(false);
            this.partGroup.ResumeLayout(false);
            this.partButtons.ResumeLayout(false);
            this.splitViewer.Panel1.ResumeLayout(false);
            this.splitViewer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitViewer)).EndInit();
            this.splitViewer.ResumeLayout(false);
            this.timelinePanel.ResumeLayout(false);
            this.transportPanel.ResumeLayout(false);
            this.transportPanel.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel topPanel;
        private System.Windows.Forms.Label clipLabel;
        private System.Windows.Forms.Button modelBtn;
        private System.Windows.Forms.Label modelLabel;
        private System.Windows.Forms.Button skeletonBtn;
        private System.Windows.Forms.Label skeletonLabel;
        private System.Windows.Forms.Button exportBtn;
        private System.Windows.Forms.Label warningLabel;
        private System.Windows.Forms.SplitContainer splitViewer;
        private System.Windows.Forms.Integration.ElementHost viewerHost;
        private System.Windows.Forms.Panel timelinePanel;
        private OpenCAGE.Popups.UserControls.AnimationTimeline timeline;
        private System.Windows.Forms.Label markerLabel;
        private System.Windows.Forms.Panel transportPanel;
        private System.Windows.Forms.Button playBtn;
        private System.Windows.Forms.Label frameLabel;
        private System.Windows.Forms.Label speedLabel;
        private System.Windows.Forms.ComboBox speedBox;
        private System.Windows.Forms.CheckBox loopCheck;
        private System.Windows.Forms.CheckBox bonesCheck;
        private System.Windows.Forms.CheckBox showMeshCheck;
        private System.Windows.Forms.CheckBox meshCheck;
        private System.Windows.Forms.Label markerCountLabel;
        private System.Windows.Forms.CheckBox rootMotionCheck;
        private System.Windows.Forms.GroupBox partGroup;
        private System.Windows.Forms.FlowLayoutPanel partPanel;
        private System.Windows.Forms.Panel partButtons;
        private System.Windows.Forms.Button showAllPartsBtn;
        private System.Windows.Forms.Button hideAllPartsBtn;
    }
}

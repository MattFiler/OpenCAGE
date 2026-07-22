namespace OpenCAGE
{
    partial class CAGEAnimationEditor
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
            this.animKeyframeValue = new System.Windows.Forms.TextBox();
            this.animKeyframeData = new System.Windows.Forms.GroupBox();
            this.deleteAnimKeyframe = new System.Windows.Forms.Button();
            this.label7 = new System.Windows.Forms.Label();
            this.graphEventData = new System.Windows.Forms.GroupBox();
            this.openGuidEntityBtn = new System.Windows.Forms.Button();
            this.graphGuidEntityName = new System.Windows.Forms.Label();
            this.deleteGraphEvent = new System.Windows.Forms.Button();
            this.label13 = new System.Windows.Forms.Label();
            this.graphEventParam1 = new System.Windows.Forms.TextBox();
            this.stringEventHint = new System.Windows.Forms.Label();
            this.animHost = new System.Windows.Forms.Integration.ElementHost();
            this.SaveEntity = new System.Windows.Forms.Button();
            this.bezierMode = new System.Windows.Forms.CheckBox();
            this.tracksHeader = new System.Windows.Forms.Label();
            this.trackTree = new System.Windows.Forms.TreeView();
            this.snapToSeconds = new System.Windows.Forms.CheckBox();
            this.snapInterval = new System.Windows.Forms.ComboBox();
            this.labelSnap = new System.Windows.Forms.Label();
            this.animKeyframeData.SuspendLayout();
            this.graphEventData.SuspendLayout();
            this.SuspendLayout();
            // 
            // animKeyframeValue
            // 
            this.animKeyframeValue.Location = new System.Drawing.Point(10, 34);
            this.animKeyframeValue.Name = "animKeyframeValue";
            this.animKeyframeValue.Size = new System.Drawing.Size(178, 20);
            this.animKeyframeValue.TabIndex = 1;
            this.animKeyframeValue.TextChanged += new System.EventHandler(this.animKeyframeValue_TextChanged);
            // 
            // animKeyframeData
            // 
            this.animKeyframeData.Controls.Add(this.deleteAnimKeyframe);
            this.animKeyframeData.Controls.Add(this.label7);
            this.animKeyframeData.Controls.Add(this.animKeyframeValue);
            this.animKeyframeData.Location = new System.Drawing.Point(863, 72);
            this.animKeyframeData.Name = "animKeyframeData";
            this.animKeyframeData.Size = new System.Drawing.Size(198, 100);
            this.animKeyframeData.TabIndex = 12;
            this.animKeyframeData.TabStop = false;
            this.animKeyframeData.Text = "Keyframe";
            this.animKeyframeData.Visible = false;
            // 
            // deleteAnimKeyframe
            // 
            this.deleteAnimKeyframe.Location = new System.Drawing.Point(10, 64);
            this.deleteAnimKeyframe.Name = "deleteAnimKeyframe";
            this.deleteAnimKeyframe.Size = new System.Drawing.Size(178, 26);
            this.deleteAnimKeyframe.TabIndex = 2;
            this.deleteAnimKeyframe.Text = "Delete Keyframe";
            this.deleteAnimKeyframe.UseVisualStyleBackColor = true;
            this.deleteAnimKeyframe.Click += new System.EventHandler(this.deleteAnimKeyframe_Click);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(8, 18);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(37, 13);
            this.label7.TabIndex = 0;
            this.label7.Text = "Value:";
            // 
            // graphEventData
            // 
            this.graphEventData.Controls.Add(this.openGuidEntityBtn);
            this.graphEventData.Controls.Add(this.graphGuidEntityName);
            this.graphEventData.Controls.Add(this.deleteGraphEvent);
            this.graphEventData.Controls.Add(this.label13);
            this.graphEventData.Controls.Add(this.graphEventParam1);
            this.graphEventData.Controls.Add(this.stringEventHint);
            this.graphEventData.Location = new System.Drawing.Point(863, 72);
            this.graphEventData.Name = "graphEventData";
            this.graphEventData.Size = new System.Drawing.Size(198, 98);
            this.graphEventData.TabIndex = 19;
            this.graphEventData.TabStop = false;
            this.graphEventData.Text = "Event";
            this.graphEventData.Visible = false;
            // 
            // openGuidEntityBtn
            // 
            this.openGuidEntityBtn.Location = new System.Drawing.Point(10, 72);
            this.openGuidEntityBtn.Name = "openGuidEntityBtn";
            this.openGuidEntityBtn.Size = new System.Drawing.Size(178, 26);
            this.openGuidEntityBtn.TabIndex = 3;
            this.openGuidEntityBtn.Text = "Open in Inspector";
            this.openGuidEntityBtn.UseVisualStyleBackColor = true;
            this.openGuidEntityBtn.Visible = false;
            this.openGuidEntityBtn.Click += new System.EventHandler(this.openGuidEntityBtn_Click);
            // 
            // graphGuidEntityName
            // 
            this.graphGuidEntityName.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.graphGuidEntityName.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(100)))), ((int)(((byte)(180)))));
            this.graphGuidEntityName.Location = new System.Drawing.Point(10, 34);
            this.graphGuidEntityName.Name = "graphGuidEntityName";
            this.graphGuidEntityName.Size = new System.Drawing.Size(178, 34);
            this.graphGuidEntityName.AutoEllipsis = true;
            this.graphGuidEntityName.TabIndex = 1;
            this.graphGuidEntityName.Text = "";
            this.graphGuidEntityName.Visible = false;
            // 
            // deleteGraphEvent
            // 
            this.deleteGraphEvent.Location = new System.Drawing.Point(10, 64);
            this.deleteGraphEvent.Name = "deleteGraphEvent";
            this.deleteGraphEvent.Size = new System.Drawing.Size(178, 26);
            this.deleteGraphEvent.TabIndex = 2;
            this.deleteGraphEvent.Text = "Delete Event";
            this.deleteGraphEvent.UseVisualStyleBackColor = true;
            this.deleteGraphEvent.Click += new System.EventHandler(this.deleteGraphEvent_Click);
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(8, 18);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(82, 13);
            this.label13.TabIndex = 0;
            this.label13.Text = "Event to trigger:";
            // 
            // graphEventParam1
            // 
            this.graphEventParam1.Location = new System.Drawing.Point(10, 34);
            this.graphEventParam1.Name = "graphEventParam1";
            this.graphEventParam1.Size = new System.Drawing.Size(178, 20);
            this.graphEventParam1.TabIndex = 1;
            this.graphEventParam1.TextChanged += new System.EventHandler(this.graphEventParam1_TextChanged);
            // 
            // stringEventHint
            // 
            this.stringEventHint.AutoSize = false;
            this.stringEventHint.ForeColor = System.Drawing.SystemColors.GrayText;
            this.stringEventHint.Location = new System.Drawing.Point(10, 56);
            this.stringEventHint.Name = "stringEventHint";
            this.stringEventHint.Size = new System.Drawing.Size(178, 28);
            this.stringEventHint.TabIndex = 4;
            this.stringEventHint.Text = "Reverse event name is generated automatically.";
            this.stringEventHint.Visible = false;
            // 
            // animHost
            // 
            this.animHost.Location = new System.Drawing.Point(232, 28);
            this.animHost.Name = "animHost";
            this.animHost.Size = new System.Drawing.Size(625, 289);
            this.animHost.TabIndex = 13;
            this.animHost.Text = "elementHost1";
            this.animHost.Child = null;
            // 
            // SaveEntity
            // 
            this.SaveEntity.Location = new System.Drawing.Point(911, 578);
            this.SaveEntity.Name = "SaveEntity";
            this.SaveEntity.Size = new System.Drawing.Size(120, 28);
            this.SaveEntity.TabIndex = 40;
            this.SaveEntity.Text = "Save";
            this.SaveEntity.UseVisualStyleBackColor = true;
            this.SaveEntity.Click += new System.EventHandler(this.SaveEntity_Click);
            // 
            // bezierMode
            // 
            this.bezierMode.AutoSize = true;
            this.bezierMode.Location = new System.Drawing.Point(300, 593);
            this.bezierMode.Name = "bezierMode";
            this.bezierMode.Size = new System.Drawing.Size(95, 17);
            this.bezierMode.TabIndex = 33;
            this.bezierMode.Text = "Bezier curves";
            this.bezierMode.UseVisualStyleBackColor = true;
            this.bezierMode.CheckedChanged += new System.EventHandler(this.bezierMode_CheckedChanged);
            // 
            // tracksHeader
            // 
            this.tracksHeader.AutoSize = true;
            this.tracksHeader.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tracksHeader.Location = new System.Drawing.Point(8, 8);
            this.tracksHeader.Name = "tracksHeader";
            this.tracksHeader.Size = new System.Drawing.Size(122, 13);
            this.tracksHeader.TabIndex = 0;
            this.tracksHeader.Text = "Animated parameters";
            // 
            // trackTree
            // 
            this.trackTree.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.trackTree.HideSelection = false;
            this.trackTree.Location = new System.Drawing.Point(8, 28);
            this.trackTree.Name = "trackTree";
            this.trackTree.Size = new System.Drawing.Size(220, 289);
            this.trackTree.TabIndex = 1;
            // 
            // snapToSeconds
            // 
            this.snapToSeconds.AutoSize = true;
            this.snapToSeconds.Location = new System.Drawing.Point(8, 593);
            this.snapToSeconds.Name = "snapToSeconds";
            this.snapToSeconds.Size = new System.Drawing.Size(52, 17);
            this.snapToSeconds.TabIndex = 30;
            this.snapToSeconds.Text = "Snap";
            this.snapToSeconds.UseVisualStyleBackColor = true;
            this.snapToSeconds.CheckedChanged += new System.EventHandler(this.snapToSeconds_CheckedChanged);
            // 
            // labelSnap
            // 
            this.labelSnap.AutoSize = true;
            this.labelSnap.Location = new System.Drawing.Point(66, 595);
            this.labelSnap.Name = "labelSnap";
            this.labelSnap.Size = new System.Drawing.Size(45, 13);
            this.labelSnap.TabIndex = 31;
            this.labelSnap.Text = "Interval:";
            // 
            // snapInterval
            // 
            this.snapInterval.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.snapInterval.FormattingEnabled = true;
            this.snapInterval.Location = new System.Drawing.Point(114, 591);
            this.snapInterval.Name = "snapInterval";
            this.snapInterval.Size = new System.Drawing.Size(90, 21);
            this.snapInterval.TabIndex = 32;
            this.snapInterval.SelectedIndexChanged += new System.EventHandler(this.snapInterval_SelectedIndexChanged);
            // 
            // CAGEAnimationEditor
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(1083, 620);
            this.Controls.Add(this.snapInterval);
            this.Controls.Add(this.labelSnap);
            this.Controls.Add(this.snapToSeconds);
            this.Controls.Add(this.bezierMode);
            this.Controls.Add(this.tracksHeader);
            this.Controls.Add(this.trackTree);
            this.Controls.Add(this.animHost);
            this.Controls.Add(this.animKeyframeData);
            this.Controls.Add(this.graphEventData);
            this.Controls.Add(this.SaveEntity);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.MinimumSize = new System.Drawing.Size(900, 520);
            this.Name = "CAGEAnimationEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "CAGEAnimation Editor";
            this.animKeyframeData.ResumeLayout(false);
            this.animKeyframeData.PerformLayout();
            this.graphEventData.ResumeLayout(false);
            this.graphEventData.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.TextBox animKeyframeValue;
        private System.Windows.Forms.GroupBox animKeyframeData;
        private System.Windows.Forms.GroupBox graphEventData;
        private System.Windows.Forms.Button openGuidEntityBtn;
        private System.Windows.Forms.Label graphGuidEntityName;
        private System.Windows.Forms.Button deleteGraphEvent;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.TextBox graphEventParam1;
        private System.Windows.Forms.Label stringEventHint;
        private System.Windows.Forms.Integration.ElementHost animHost;
        private System.Windows.Forms.Button SaveEntity;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button deleteAnimKeyframe;
        private System.Windows.Forms.Label tracksHeader;
        private System.Windows.Forms.TreeView trackTree;
        private System.Windows.Forms.CheckBox bezierMode;
        private System.Windows.Forms.CheckBox snapToSeconds;
        private System.Windows.Forms.ComboBox snapInterval;
        private System.Windows.Forms.Label labelSnap;
    }
}

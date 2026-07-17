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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CAGEAnimationEditor));
            this.animKeyframeValue = new System.Windows.Forms.TextBox();
            this.animKeyframeData = new System.Windows.Forms.GroupBox();
            this.deleteAnimKeyframe = new System.Windows.Forms.Button();
            this.label7 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.animKeyframeMode = new System.Windows.Forms.ComboBox();
            this.label11 = new System.Windows.Forms.Label();
            this.graphEventData = new System.Windows.Forms.GroupBox();
            this.deleteGraphEvent = new System.Windows.Forms.Button();
            this.label12 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.graphEventParam2 = new System.Windows.Forms.TextBox();
            this.graphEventParam1 = new System.Windows.Forms.TextBox();
            this.animHost = new System.Windows.Forms.Integration.ElementHost();
            this.SaveEntity = new System.Windows.Forms.Button();
            this.eventKeyframeData = new System.Windows.Forms.GroupBox();
            this.deleteEventKeyframe = new System.Windows.Forms.Button();
            this.label9 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.eventParam2 = new System.Windows.Forms.TextBox();
            this.eventParam1 = new System.Windows.Forms.TextBox();
            this.eventHost = new System.Windows.Forms.Integration.ElementHost();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.addNewEntityRef = new System.Windows.Forms.Button();
            this.addAnimationTrack = new System.Windows.Forms.Button();
            this.deleteAnimationTrack = new System.Windows.Forms.Button();
            this.entityList = new System.Windows.Forms.ComboBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.addEventTrack = new System.Windows.Forms.Button();
            this.deleteEventTrack = new System.Windows.Forms.Button();
            this.animLength = new System.Windows.Forms.TextBox();
            this.editAnimLength = new System.Windows.Forms.Button();
            this.label10 = new System.Windows.Forms.Label();
            this.animKeyframeData.SuspendLayout();
            this.graphEventData.SuspendLayout();
            this.eventKeyframeData.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // animKeyframeValue
            // 
            this.animKeyframeValue.Location = new System.Drawing.Point(9, 30);
            this.animKeyframeValue.Name = "animKeyframeValue";
            this.animKeyframeValue.Size = new System.Drawing.Size(181, 20);
            this.animKeyframeValue.TabIndex = 9;
            this.animKeyframeValue.TextChanged += new System.EventHandler(this.animKeyframeValue_TextChanged);
            // 
            // animKeyframeData
            // 
            this.animKeyframeData.Controls.Add(this.deleteAnimKeyframe);
            this.animKeyframeData.Controls.Add(this.label7);
            this.animKeyframeData.Controls.Add(this.label5);
            this.animKeyframeData.Controls.Add(this.animKeyframeMode);
            this.animKeyframeData.Controls.Add(this.label11);
            this.animKeyframeData.Controls.Add(this.animKeyframeValue);
            this.animKeyframeData.Location = new System.Drawing.Point(863, 47);
            this.animKeyframeData.Name = "animKeyframeData";
            this.animKeyframeData.Size = new System.Drawing.Size(198, 176);
            this.animKeyframeData.TabIndex = 12;
            this.animKeyframeData.TabStop = false;
            this.animKeyframeData.Text = "Selected Keyframe Data";
            // 
            // deleteAnimKeyframe
            // 
            this.deleteAnimKeyframe.Location = new System.Drawing.Point(6, 144);
            this.deleteAnimKeyframe.Name = "deleteAnimKeyframe";
            this.deleteAnimKeyframe.Size = new System.Drawing.Size(184, 26);
            this.deleteAnimKeyframe.TabIndex = 16;
            this.deleteAnimKeyframe.Text = "Delete Keyframe";
            this.deleteAnimKeyframe.UseVisualStyleBackColor = true;
            this.deleteAnimKeyframe.Click += new System.EventHandler(this.deleteAnimKeyframe_Click);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(6, 16);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(37, 13);
            this.label7.TabIndex = 22;
            this.label7.Text = "Value:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(6, 60);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(71, 13);
            this.label5.TabIndex = 20;
            this.label5.Text = "Interpolation:";
            // 
            // animKeyframeMode
            // 
            this.animKeyframeMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.animKeyframeMode.FormattingEnabled = true;
            this.animKeyframeMode.Location = new System.Drawing.Point(9, 76);
            this.animKeyframeMode.Name = "animKeyframeMode";
            this.animKeyframeMode.Size = new System.Drawing.Size(181, 21);
            this.animKeyframeMode.TabIndex = 11;
            this.animKeyframeMode.SelectedIndexChanged += new System.EventHandler(this.animKeyframeMode_SelectedIndexChanged);
            // 
            // label11
            // 
            this.label11.Location = new System.Drawing.Point(6, 104);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(184, 30);
            this.label11.TabIndex = 24;
            this.label11.Text = "Set mode to Bezier, then drag the square handles on the graph to shape the curve." +
    "";
            // 
            // graphEventData
            // 
            this.graphEventData.Controls.Add(this.deleteGraphEvent);
            this.graphEventData.Controls.Add(this.label12);
            this.graphEventData.Controls.Add(this.label13);
            this.graphEventData.Controls.Add(this.graphEventParam2);
            this.graphEventData.Controls.Add(this.graphEventParam1);
            this.graphEventData.Location = new System.Drawing.Point(863, 47);
            this.graphEventData.Name = "graphEventData";
            this.graphEventData.Size = new System.Drawing.Size(198, 176);
            this.graphEventData.TabIndex = 19;
            this.graphEventData.TabStop = false;
            this.graphEventData.Text = "Selected Event";
            this.graphEventData.Visible = false;
            // 
            // deleteGraphEvent
            // 
            this.deleteGraphEvent.Location = new System.Drawing.Point(6, 144);
            this.deleteGraphEvent.Name = "deleteGraphEvent";
            this.deleteGraphEvent.Size = new System.Drawing.Size(184, 26);
            this.deleteGraphEvent.TabIndex = 23;
            this.deleteGraphEvent.Text = "Delete Event";
            this.deleteGraphEvent.UseVisualStyleBackColor = true;
            this.deleteGraphEvent.Click += new System.EventHandler(this.deleteGraphEvent_Click);
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(6, 53);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(124, 13);
            this.label12.TabIndex = 24;
            this.label12.Text = "Reverse event to trigger:";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(6, 16);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(82, 13);
            this.label13.TabIndex = 23;
            this.label13.Text = "Event to trigger:";
            // 
            // graphEventParam2
            // 
            this.graphEventParam2.Location = new System.Drawing.Point(6, 67);
            this.graphEventParam2.Name = "graphEventParam2";
            this.graphEventParam2.Size = new System.Drawing.Size(184, 20);
            this.graphEventParam2.TabIndex = 10;
            this.graphEventParam2.TextChanged += new System.EventHandler(this.graphEventParam2_TextChanged);
            // 
            // graphEventParam1
            // 
            this.graphEventParam1.Location = new System.Drawing.Point(6, 30);
            this.graphEventParam1.Name = "graphEventParam1";
            this.graphEventParam1.Size = new System.Drawing.Size(184, 20);
            this.graphEventParam1.TabIndex = 9;
            this.graphEventParam1.TextChanged += new System.EventHandler(this.graphEventParam1_TextChanged);
            // 
            // animHost
            // 
            this.animHost.Location = new System.Drawing.Point(6, 46);
            this.animHost.Name = "animHost";
            this.animHost.Size = new System.Drawing.Size(851, 262);
            this.animHost.TabIndex = 13;
            this.animHost.Text = "elementHost1";
            this.animHost.Child = null;
            // 
            // SaveEntity
            // 
            this.SaveEntity.Location = new System.Drawing.Point(911, 578);
            this.SaveEntity.Name = "SaveEntity";
            this.SaveEntity.Size = new System.Drawing.Size(167, 36);
            this.SaveEntity.TabIndex = 12;
            this.SaveEntity.Text = "Save";
            this.SaveEntity.UseVisualStyleBackColor = true;
            this.SaveEntity.Click += new System.EventHandler(this.SaveEntity_Click);
            // 
            // eventKeyframeData
            // 
            this.eventKeyframeData.Controls.Add(this.deleteEventKeyframe);
            this.eventKeyframeData.Controls.Add(this.label9);
            this.eventKeyframeData.Controls.Add(this.label8);
            this.eventKeyframeData.Controls.Add(this.eventParam2);
            this.eventKeyframeData.Controls.Add(this.eventParam1);
            this.eventKeyframeData.Location = new System.Drawing.Point(863, 19);
            this.eventKeyframeData.Name = "eventKeyframeData";
            this.eventKeyframeData.Size = new System.Drawing.Size(198, 127);
            this.eventKeyframeData.TabIndex = 14;
            this.eventKeyframeData.TabStop = false;
            this.eventKeyframeData.Text = "Selected Keyframe Data";
            // 
            // deleteEventKeyframe
            // 
            this.deleteEventKeyframe.Location = new System.Drawing.Point(6, 93);
            this.deleteEventKeyframe.Name = "deleteEventKeyframe";
            this.deleteEventKeyframe.Size = new System.Drawing.Size(184, 26);
            this.deleteEventKeyframe.TabIndex = 23;
            this.deleteEventKeyframe.Text = "Delete Keyframe";
            this.deleteEventKeyframe.UseVisualStyleBackColor = true;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(6, 53);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(124, 13);
            this.label9.TabIndex = 24;
            this.label9.Text = "Reverse event to trigger:";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(6, 16);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(82, 13);
            this.label8.TabIndex = 23;
            this.label8.Text = "Event to trigger:";
            // 
            // eventParam2
            // 
            this.eventParam2.Location = new System.Drawing.Point(6, 67);
            this.eventParam2.Name = "eventParam2";
            this.eventParam2.Size = new System.Drawing.Size(184, 20);
            this.eventParam2.TabIndex = 10;
            // 
            // eventParam1
            // 
            this.eventParam1.Location = new System.Drawing.Point(6, 30);
            this.eventParam1.Name = "eventParam1";
            this.eventParam1.Size = new System.Drawing.Size(184, 20);
            this.eventParam1.TabIndex = 9;
            // 
            // eventHost
            // 
            this.eventHost.Location = new System.Drawing.Point(6, 19);
            this.eventHost.Name = "eventHost";
            this.eventHost.Size = new System.Drawing.Size(851, 213);
            this.eventHost.TabIndex = 15;
            this.eventHost.Text = "elementHost1";
            this.eventHost.Child = null;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.addNewEntityRef);
            this.groupBox1.Controls.Add(this.addAnimationTrack);
            this.groupBox1.Controls.Add(this.deleteAnimationTrack);
            this.groupBox1.Controls.Add(this.addEventTrack);
            this.groupBox1.Controls.Add(this.deleteEventTrack);
            this.groupBox1.Controls.Add(this.entityList);
            this.groupBox1.Controls.Add(this.animHost);
            this.groupBox1.Controls.Add(this.animKeyframeData);
            this.groupBox1.Controls.Add(this.graphEventData);
            this.groupBox1.Location = new System.Drawing.Point(7, 6);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(1071, 560);
            this.groupBox1.TabIndex = 16;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Animation Sequences";
            // 
            // addNewEntityRef
            // 
            this.addNewEntityRef.Location = new System.Drawing.Point(863, 17);
            this.addNewEntityRef.Name = "addNewEntityRef";
            this.addNewEntityRef.Size = new System.Drawing.Size(198, 23);
            this.addNewEntityRef.TabIndex = 18;
            this.addNewEntityRef.Text = "Add New Entity Link";
            this.addNewEntityRef.UseVisualStyleBackColor = true;
            this.addNewEntityRef.Click += new System.EventHandler(this.addNewEntityRef_Click);
            // 
            // addAnimationTrack
            // 
            this.addAnimationTrack.Location = new System.Drawing.Point(863, 450);
            this.addAnimationTrack.Name = "addAnimationTrack";
            this.addAnimationTrack.Size = new System.Drawing.Size(198, 23);
            this.addAnimationTrack.TabIndex = 17;
            this.addAnimationTrack.Text = "Add Animation Track";
            this.addAnimationTrack.UseVisualStyleBackColor = true;
            this.addAnimationTrack.Click += new System.EventHandler(this.addAnimationTrack_Click);
            // 
            // deleteAnimationTrack
            // 
            this.deleteAnimationTrack.Location = new System.Drawing.Point(863, 475);
            this.deleteAnimationTrack.Name = "deleteAnimationTrack";
            this.deleteAnimationTrack.Size = new System.Drawing.Size(198, 23);
            this.deleteAnimationTrack.TabIndex = 16;
            this.deleteAnimationTrack.Text = "Delete Animation Track";
            this.deleteAnimationTrack.UseVisualStyleBackColor = true;
            this.deleteAnimationTrack.Click += new System.EventHandler(this.deleteAnimationTrack_Click);
            // 
            // entityList
            // 
            this.entityList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.entityList.FormattingEnabled = true;
            this.entityList.Location = new System.Drawing.Point(6, 19);
            this.entityList.Name = "entityList";
            this.entityList.Size = new System.Drawing.Size(851, 21);
            this.entityList.TabIndex = 14;
            this.entityList.SelectedIndexChanged += new System.EventHandler(this.entityList_SelectedIndexChanged);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.eventHost);
            this.groupBox2.Controls.Add(this.eventKeyframeData);
            this.groupBox2.Location = new System.Drawing.Point(7, 330);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(1071, 242);
            this.groupBox2.TabIndex = 17;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Event Sequences";
            this.groupBox2.Visible = false;
            // 
            // addEventTrack
            // 
            this.addEventTrack.Location = new System.Drawing.Point(863, 504);
            this.addEventTrack.Name = "addEventTrack";
            this.addEventTrack.Size = new System.Drawing.Size(198, 23);
            this.addEventTrack.TabIndex = 19;
            this.addEventTrack.Text = "Add Event Track";
            this.addEventTrack.UseVisualStyleBackColor = true;
            this.addEventTrack.Click += new System.EventHandler(this.addEventTrack_Click);
            // 
            // deleteEventTrack
            // 
            this.deleteEventTrack.Location = new System.Drawing.Point(863, 529);
            this.deleteEventTrack.Name = "deleteEventTrack";
            this.deleteEventTrack.Size = new System.Drawing.Size(198, 23);
            this.deleteEventTrack.TabIndex = 18;
            this.deleteEventTrack.Text = "Delete Event Track";
            this.deleteEventTrack.UseVisualStyleBackColor = true;
            this.deleteEventTrack.Click += new System.EventHandler(this.deleteEventTrack_Click);
            // 
            // animLength
            // 
            this.animLength.Location = new System.Drawing.Point(7, 591);
            this.animLength.Name = "animLength";
            this.animLength.Size = new System.Drawing.Size(184, 20);
            this.animLength.TabIndex = 25;
            this.animLength.TextChanged += new System.EventHandler(this.animLength_TextChanged);
            // 
            // editAnimLength
            // 
            this.editAnimLength.Location = new System.Drawing.Point(197, 590);
            this.editAnimLength.Name = "editAnimLength";
            this.editAnimLength.Size = new System.Drawing.Size(94, 23);
            this.editAnimLength.TabIndex = 26;
            this.editAnimLength.Text = "Edit Length";
            this.editAnimLength.UseVisualStyleBackColor = true;
            this.editAnimLength.Click += new System.EventHandler(this.editAnimLength_Click);
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(4, 577);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(105, 13);
            this.label10.TabIndex = 25;
            this.label10.Text = "Total time (seconds):";
            // 
            // CAGEAnimationEditor
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(1083, 620);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.editAnimLength);
            this.Controls.Add(this.animLength);
            this.Controls.Add(this.groupBox1);
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
            this.eventKeyframeData.ResumeLayout(false);
            this.eventKeyframeData.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.TextBox animKeyframeValue;
        private System.Windows.Forms.GroupBox animKeyframeData;
        private System.Windows.Forms.GroupBox graphEventData;
        private System.Windows.Forms.Button deleteGraphEvent;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.TextBox graphEventParam2;
        private System.Windows.Forms.TextBox graphEventParam1;
        private System.Windows.Forms.Integration.ElementHost animHost;
        private System.Windows.Forms.Button SaveEntity;
        private System.Windows.Forms.GroupBox eventKeyframeData;
        private System.Windows.Forms.TextBox eventParam2;
        private System.Windows.Forms.TextBox eventParam1;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox animKeyframeMode;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Integration.ElementHost eventHost;
        private System.Windows.Forms.Button deleteAnimKeyframe;
        private System.Windows.Forms.Button deleteEventKeyframe;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button addAnimationTrack;
        private System.Windows.Forms.Button deleteAnimationTrack;
        private System.Windows.Forms.ComboBox entityList;
        private System.Windows.Forms.Button addEventTrack;
        private System.Windows.Forms.Button deleteEventTrack;
        private System.Windows.Forms.Button addNewEntityRef;
        private System.Windows.Forms.TextBox animLength;
        private System.Windows.Forms.Button editAnimLength;
        private System.Windows.Forms.Label label10;
    }
}

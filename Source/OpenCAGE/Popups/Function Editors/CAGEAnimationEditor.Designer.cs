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
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.endVelY = new System.Windows.Forms.TextBox();
            this.endVelX = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.startVelY = new System.Windows.Forms.TextBox();
            this.startVelX = new System.Windows.Forms.TextBox();
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
            this.animKeyframeData.Controls.Add(this.label6);
            this.animKeyframeData.Controls.Add(this.label5);
            this.animKeyframeData.Controls.Add(this.label3);
            this.animKeyframeData.Controls.Add(this.label4);
            this.animKeyframeData.Controls.Add(this.endVelY);
            this.animKeyframeData.Controls.Add(this.endVelX);
            this.animKeyframeData.Controls.Add(this.label2);
            this.animKeyframeData.Controls.Add(this.label1);
            this.animKeyframeData.Controls.Add(this.startVelY);
            this.animKeyframeData.Controls.Add(this.startVelX);
            this.animKeyframeData.Controls.Add(this.animKeyframeValue);
            this.animKeyframeData.Location = new System.Drawing.Point(863, 47);
            this.animKeyframeData.Name = "animKeyframeData";
            this.animKeyframeData.Size = new System.Drawing.Size(198, 164);
            this.animKeyframeData.TabIndex = 12;
            this.animKeyframeData.TabStop = false;
            this.animKeyframeData.Text = "Selected Keyframe Data";
            // 
            // deleteAnimKeyframe
            // 
            this.deleteAnimKeyframe.Location = new System.Drawing.Point(6, 132);
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
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(6, 92);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(68, 13);
            this.label6.TabIndex = 21;
            this.label6.Text = "End velocity:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(6, 53);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(71, 13);
            this.label5.TabIndex = 20;
            this.label5.Text = "Start velocity:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(100, 110);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(17, 13);
            this.label3.TabIndex = 19;
            this.label3.Text = "Y:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(6, 110);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(17, 13);
            this.label4.TabIndex = 18;
            this.label4.Text = "X:";
            // 
            // endVelY
            // 
            this.endVelY.Location = new System.Drawing.Point(118, 106);
            this.endVelY.Name = "endVelY";
            this.endVelY.Size = new System.Drawing.Size(72, 20);
            this.endVelY.TabIndex = 17;
            this.endVelY.TextChanged += new System.EventHandler(this.endVelY_TextChanged);
            // 
            // endVelX
            // 
            this.endVelX.Location = new System.Drawing.Point(23, 106);
            this.endVelX.Name = "endVelX";
            this.endVelX.Size = new System.Drawing.Size(72, 20);
            this.endVelX.TabIndex = 16;
            this.endVelX.TextChanged += new System.EventHandler(this.endVelX_TextChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(100, 73);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(17, 13);
            this.label2.TabIndex = 15;
            this.label2.Text = "Y:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 73);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(17, 13);
            this.label1.TabIndex = 14;
            this.label1.Text = "X:";
            // 
            // startVelY
            // 
            this.startVelY.Location = new System.Drawing.Point(118, 69);
            this.startVelY.Name = "startVelY";
            this.startVelY.Size = new System.Drawing.Size(72, 20);
            this.startVelY.TabIndex = 11;
            this.startVelY.TextChanged += new System.EventHandler(this.startVelY_TextChanged);
            // 
            // startVelX
            // 
            this.startVelX.Location = new System.Drawing.Point(23, 69);
            this.startVelX.Name = "startVelX";
            this.startVelX.Size = new System.Drawing.Size(72, 20);
            this.startVelX.TabIndex = 10;
            this.startVelX.TextChanged += new System.EventHandler(this.startVelX_TextChanged);
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
            this.deleteEventKeyframe.Click += new System.EventHandler(this.deleteEventKeyframe_Click);
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
            this.eventParam2.TextChanged += new System.EventHandler(this.eventParam2_TextChanged);
            // 
            // eventParam1
            // 
            this.eventParam1.Location = new System.Drawing.Point(6, 30);
            this.eventParam1.Name = "eventParam1";
            this.eventParam1.Size = new System.Drawing.Size(184, 20);
            this.eventParam1.TabIndex = 9;
            this.eventParam1.TextChanged += new System.EventHandler(this.eventParam1_TextChanged);
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
            this.groupBox1.Controls.Add(this.entityList);
            this.groupBox1.Controls.Add(this.animHost);
            this.groupBox1.Controls.Add(this.animKeyframeData);
            this.groupBox1.Location = new System.Drawing.Point(7, 6);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(1071, 318);
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
            this.addAnimationTrack.Location = new System.Drawing.Point(863, 260);
            this.addAnimationTrack.Name = "addAnimationTrack";
            this.addAnimationTrack.Size = new System.Drawing.Size(198, 23);
            this.addAnimationTrack.TabIndex = 17;
            this.addAnimationTrack.Text = "Add Animation Track";
            this.addAnimationTrack.UseVisualStyleBackColor = true;
            this.addAnimationTrack.Click += new System.EventHandler(this.addAnimationTrack_Click);
            // 
            // deleteAnimationTrack
            // 
            this.deleteAnimationTrack.Location = new System.Drawing.Point(863, 285);
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
            this.groupBox2.Controls.Add(this.addEventTrack);
            this.groupBox2.Controls.Add(this.deleteEventTrack);
            this.groupBox2.Controls.Add(this.eventHost);
            this.groupBox2.Controls.Add(this.eventKeyframeData);
            this.groupBox2.Location = new System.Drawing.Point(7, 330);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(1071, 242);
            this.groupBox2.TabIndex = 17;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Event Sequences";
            // 
            // addEventTrack
            // 
            this.addEventTrack.Location = new System.Drawing.Point(863, 184);
            this.addEventTrack.Name = "addEventTrack";
            this.addEventTrack.Size = new System.Drawing.Size(198, 23);
            this.addEventTrack.TabIndex = 19;
            this.addEventTrack.Text = "Add Event Track";
            this.addEventTrack.UseVisualStyleBackColor = true;
            this.addEventTrack.Click += new System.EventHandler(this.addEventTrack_Click);
            // 
            // deleteEventTrack
            // 
            this.deleteEventTrack.Location = new System.Drawing.Point(863, 209);
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
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.SaveEntity);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "CAGEAnimationEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "CAGEAnimation Editor";
            this.animKeyframeData.ResumeLayout(false);
            this.animKeyframeData.PerformLayout();
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
        private System.Windows.Forms.Integration.ElementHost animHost;
        private System.Windows.Forms.Button SaveEntity;
        private System.Windows.Forms.TextBox startVelY;
        private System.Windows.Forms.TextBox startVelX;
        private System.Windows.Forms.GroupBox eventKeyframeData;
        private System.Windows.Forms.TextBox eventParam2;
        private System.Windows.Forms.TextBox eventParam1;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox endVelY;
        private System.Windows.Forms.TextBox endVelX;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
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

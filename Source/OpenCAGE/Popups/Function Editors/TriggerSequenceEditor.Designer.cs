namespace CommandsEditor
{
    partial class TriggerSequenceEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TriggerSequenceEditor));
            System.Windows.Forms.ListViewGroup listViewGroup1 = new System.Windows.Forms.ListViewGroup("Target", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup2 = new System.Windows.Forms.ListViewGroup("State", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup3 = new System.Windows.Forms.ListViewGroup("Input", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup4 = new System.Windows.Forms.ListViewGroup("Output", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup5 = new System.Windows.Forms.ListViewGroup("Parameter", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup6 = new System.Windows.Forms.ListViewGroup("Internal", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup7 = new System.Windows.Forms.ListViewGroup("Reference", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup8 = new System.Windows.Forms.ListViewGroup("Method", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup9 = new System.Windows.Forms.ListViewGroup("Finished", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup10 = new System.Windows.Forms.ListViewGroup("Relay", System.Windows.Forms.HorizontalAlignment.Left);
            this.trigger_list = new System.Windows.Forms.ListBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.button2 = new System.Windows.Forms.Button();
            this.moveDown = new System.Windows.Forms.Button();
            this.entity_list = new System.Windows.Forms.ListView();
            this.funcHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.inheritHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.addNewTrigger = new System.Windows.Forms.Button();
            this.moveUp = new System.Windows.Forms.Button();
            this.selectedEntityDetails = new System.Windows.Forms.GroupBox();
            this.button3 = new System.Windows.Forms.Button();
            this.entityTriggerDelay = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.entityHierarchy = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.selectEntToPointTo = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.selectedTriggerDetails = new System.Windows.Forms.GroupBox();
            this.saveTrigger = new System.Windows.Forms.Button();
            this.triggerStartParam = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.deleteParamTrigger = new System.Windows.Forms.Button();
            this.addNewParamTrigger = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.groupBox1.SuspendLayout();
            this.selectedEntityDetails.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.selectedTriggerDetails.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // trigger_list
            // 
            this.trigger_list.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.trigger_list.FormattingEnabled = true;
            this.trigger_list.HorizontalScrollbar = true;
            this.trigger_list.Location = new System.Drawing.Point(6, 19);
            this.trigger_list.Name = "trigger_list";
            this.trigger_list.Size = new System.Drawing.Size(695, 290);
            this.trigger_list.TabIndex = 2;
            this.trigger_list.SelectedIndexChanged += new System.EventHandler(this.trigger_list_SelectedIndexChanged);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.button2);
            this.groupBox1.Controls.Add(this.moveDown);
            this.groupBox1.Controls.Add(this.entity_list);
            this.groupBox1.Controls.Add(this.addNewTrigger);
            this.groupBox1.Controls.Add(this.moveUp);
            this.groupBox1.Controls.Add(this.selectedEntityDetails);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox1.Location = new System.Drawing.Point(0, 0);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(1194, 318);
            this.groupBox1.TabIndex = 4;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Entities";
            // 
            // button2
            // 
            this.button2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button2.Location = new System.Drawing.Point(924, 286);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(144, 26);
            this.button2.TabIndex = 8;
            this.button2.Text = "Remove Checked";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // moveDown
            // 
            this.moveDown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.moveDown.Enabled = false;
            this.moveDown.Image = ((System.Drawing.Image)(resources.GetObject("moveDown.Image")));
            this.moveDown.Location = new System.Drawing.Point(707, 222);
            this.moveDown.Name = "moveDown";
            this.moveDown.Size = new System.Drawing.Size(27, 27);
            this.moveDown.TabIndex = 5;
            this.toolTip1.SetToolTip(this.moveDown, "Move selected down in the list");
            this.moveDown.UseVisualStyleBackColor = true;
            this.moveDown.Click += new System.EventHandler(this.moveDown_Click);
            // 
            // entity_list
            // 
            this.entity_list.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.entity_list.CheckBoxes = true;
            this.entity_list.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.funcHeader,
            this.inheritHeader});
            this.entity_list.FullRowSelect = true;
            listViewGroup1.Header = "Target";
            listViewGroup1.Name = "Target";
            listViewGroup2.Header = "State";
            listViewGroup2.Name = "State";
            listViewGroup3.Header = "Input";
            listViewGroup3.Name = "Input";
            listViewGroup4.Header = "Output";
            listViewGroup4.Name = "Output";
            listViewGroup5.Header = "Parameter";
            listViewGroup5.Name = "Parameter";
            listViewGroup6.Header = "Internal";
            listViewGroup6.Name = "Internal";
            listViewGroup7.Header = "Reference";
            listViewGroup7.Name = "Reference";
            listViewGroup8.Header = "Method";
            listViewGroup8.Name = "Method";
            listViewGroup9.Header = "Finished";
            listViewGroup9.Name = "Finished";
            listViewGroup10.Header = "Relay";
            listViewGroup10.Name = "Relay";
            this.entity_list.Groups.AddRange(new System.Windows.Forms.ListViewGroup[] {
            listViewGroup1,
            listViewGroup2,
            listViewGroup3,
            listViewGroup4,
            listViewGroup5,
            listViewGroup6,
            listViewGroup7,
            listViewGroup8,
            listViewGroup9,
            listViewGroup10});
            this.entity_list.HideSelection = false;
            this.entity_list.Location = new System.Drawing.Point(6, 19);
            this.entity_list.MultiSelect = false;
            this.entity_list.Name = "entity_list";
            this.entity_list.Size = new System.Drawing.Size(695, 293);
            this.entity_list.TabIndex = 181;
            this.entity_list.UseCompatibleStateImageBehavior = false;
            this.entity_list.View = System.Windows.Forms.View.Details;
            this.entity_list.SelectedIndexChanged += new System.EventHandler(this.entity_list_SelectedIndexChanged);
            // 
            // funcHeader
            // 
            this.funcHeader.Text = "Entity";
            this.funcHeader.Width = 595;
            // 
            // inheritHeader
            // 
            this.inheritHeader.Text = "Delay";
            this.inheritHeader.Width = 48;
            // 
            // addNewTrigger
            // 
            this.addNewTrigger.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.addNewTrigger.Location = new System.Drawing.Point(707, 286);
            this.addNewTrigger.Name = "addNewTrigger";
            this.addNewTrigger.Size = new System.Drawing.Size(211, 26);
            this.addNewTrigger.TabIndex = 2;
            this.addNewTrigger.Text = "Add New Entity Reference";
            this.addNewTrigger.UseVisualStyleBackColor = true;
            this.addNewTrigger.Click += new System.EventHandler(this.addNewEntity_Click);
            // 
            // moveUp
            // 
            this.moveUp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.moveUp.Enabled = false;
            this.moveUp.Image = ((System.Drawing.Image)(resources.GetObject("moveUp.Image")));
            this.moveUp.Location = new System.Drawing.Point(707, 192);
            this.moveUp.Name = "moveUp";
            this.moveUp.Size = new System.Drawing.Size(27, 27);
            this.moveUp.TabIndex = 4;
            this.toolTip1.SetToolTip(this.moveUp, "Move selected up in the list");
            this.moveUp.UseVisualStyleBackColor = true;
            this.moveUp.Click += new System.EventHandler(this.moveUp_Click);
            // 
            // selectedEntityDetails
            // 
            this.selectedEntityDetails.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.selectedEntityDetails.Controls.Add(this.button3);
            this.selectedEntityDetails.Controls.Add(this.entityTriggerDelay);
            this.selectedEntityDetails.Controls.Add(this.label2);
            this.selectedEntityDetails.Controls.Add(this.entityHierarchy);
            this.selectedEntityDetails.Controls.Add(this.label1);
            this.selectedEntityDetails.Controls.Add(this.selectEntToPointTo);
            this.selectedEntityDetails.Location = new System.Drawing.Point(707, 13);
            this.selectedEntityDetails.Name = "selectedEntityDetails";
            this.selectedEntityDetails.Size = new System.Drawing.Size(481, 129);
            this.selectedEntityDetails.TabIndex = 1;
            this.selectedEntityDetails.TabStop = false;
            this.selectedEntityDetails.Text = "Selected Entity Details";
            // 
            // button3
            // 
            this.button3.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.button3.Location = new System.Drawing.Point(287, 41);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(68, 23);
            this.button3.TabIndex = 7;
            this.button3.Text = "Go To";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // entityTriggerDelay
            // 
            this.entityTriggerDelay.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.entityTriggerDelay.Location = new System.Drawing.Point(17, 87);
            this.entityTriggerDelay.Name = "entityTriggerDelay";
            this.entityTriggerDelay.Size = new System.Drawing.Size(444, 20);
            this.entityTriggerDelay.TabIndex = 6;
            this.entityTriggerDelay.TextChanged += new System.EventHandler(this.triggerDelay_TextChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(16, 71);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(218, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "Time To Wait Before Triggering (In Seconds)";
            // 
            // entityHierarchy
            // 
            this.entityHierarchy.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.entityHierarchy.Location = new System.Drawing.Point(17, 42);
            this.entityHierarchy.Name = "entityHierarchy";
            this.entityHierarchy.ReadOnly = true;
            this.entityHierarchy.Size = new System.Drawing.Size(264, 20);
            this.entityHierarchy.TabIndex = 3;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(16, 26);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(85, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Entity To Trigger";
            // 
            // selectEntToPointTo
            // 
            this.selectEntToPointTo.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.selectEntToPointTo.Location = new System.Drawing.Point(361, 41);
            this.selectEntToPointTo.Name = "selectEntToPointTo";
            this.selectEntToPointTo.Size = new System.Drawing.Size(100, 23);
            this.selectEntToPointTo.TabIndex = 1;
            this.selectEntToPointTo.Text = "Select New";
            this.selectEntToPointTo.UseVisualStyleBackColor = true;
            this.selectEntToPointTo.Click += new System.EventHandler(this.selectEntToPointTo_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.selectedTriggerDetails);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.deleteParamTrigger);
            this.groupBox2.Controls.Add(this.trigger_list);
            this.groupBox2.Controls.Add(this.addNewParamTrigger);
            this.groupBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox2.Location = new System.Drawing.Point(0, 0);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(1194, 314);
            this.groupBox2.TabIndex = 5;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Triggers";
            // 
            // selectedTriggerDetails
            // 
            this.selectedTriggerDetails.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.selectedTriggerDetails.Controls.Add(this.saveTrigger);
            this.selectedTriggerDetails.Controls.Add(this.triggerStartParam);
            this.selectedTriggerDetails.Controls.Add(this.label5);
            this.selectedTriggerDetails.Location = new System.Drawing.Point(707, 13);
            this.selectedTriggerDetails.Name = "selectedTriggerDetails";
            this.selectedTriggerDetails.Size = new System.Drawing.Size(481, 109);
            this.selectedTriggerDetails.TabIndex = 7;
            this.selectedTriggerDetails.TabStop = false;
            this.selectedTriggerDetails.Text = "Selected Trigger Details";
            // 
            // saveTrigger
            // 
            this.saveTrigger.Location = new System.Drawing.Point(369, 68);
            this.saveTrigger.Name = "saveTrigger";
            this.saveTrigger.Size = new System.Drawing.Size(92, 23);
            this.saveTrigger.TabIndex = 7;
            this.saveTrigger.Text = "Save";
            this.saveTrigger.UseVisualStyleBackColor = true;
            this.saveTrigger.Click += new System.EventHandler(this.saveTrigger_Click);
            // 
            // triggerStartParam
            // 
            this.triggerStartParam.Location = new System.Drawing.Point(17, 42);
            this.triggerStartParam.Name = "triggerStartParam";
            this.triggerStartParam.Size = new System.Drawing.Size(444, 20);
            this.triggerStartParam.TabIndex = 3;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(16, 26);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(107, 13);
            this.label5.TabIndex = 2;
            this.label5.Text = "Parameter To Trigger";
            // 
            // label3
            // 
            this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(883, 250);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(239, 52);
            this.label3.TabIndex = 6;
            this.label3.Text = "This is a list of supported start/end parameters\r\nwhich can be triggered by this " +
    "TriggerSequence. \r\n\r\nWill apply to all entities in the list above.";
            // 
            // deleteParamTrigger
            // 
            this.deleteParamTrigger.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.deleteParamTrigger.Location = new System.Drawing.Point(707, 282);
            this.deleteParamTrigger.Name = "deleteParamTrigger";
            this.deleteParamTrigger.Size = new System.Drawing.Size(170, 26);
            this.deleteParamTrigger.TabIndex = 5;
            this.deleteParamTrigger.Text = "Delete Selected Trigger";
            this.deleteParamTrigger.UseVisualStyleBackColor = true;
            this.deleteParamTrigger.Click += new System.EventHandler(this.deleteParamTrigger_Click);
            // 
            // addNewParamTrigger
            // 
            this.addNewParamTrigger.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.addNewParamTrigger.Location = new System.Drawing.Point(707, 250);
            this.addNewParamTrigger.Name = "addNewParamTrigger";
            this.addNewParamTrigger.Size = new System.Drawing.Size(170, 26);
            this.addNewParamTrigger.TabIndex = 4;
            this.addNewParamTrigger.Text = "Add New Trigger";
            this.addNewParamTrigger.UseVisualStyleBackColor = true;
            this.addNewParamTrigger.Click += new System.EventHandler(this.addNewParamTrigger_Click);
            // 
            // button1
            // 
            this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button1.Location = new System.Drawing.Point(1063, 654);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(143, 32);
            this.button1.TabIndex = 6;
            this.button1.Text = "Close";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(11, 11);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.groupBox1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.groupBox2);
            this.splitContainer1.Size = new System.Drawing.Size(1194, 636);
            this.splitContainer1.SplitterDistance = 318;
            this.splitContainer1.TabIndex = 7;
            // 
            // TriggerSequenceEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1217, 695);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.button1);
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(2000, 2000);
            this.MinimumSize = new System.Drawing.Size(700, 690);
            this.Name = "TriggerSequenceEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "TriggerSequence Editor";
            this.groupBox1.ResumeLayout(false);
            this.selectedEntityDetails.ResumeLayout(false);
            this.selectedEntityDetails.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.selectedTriggerDetails.ResumeLayout(false);
            this.selectedTriggerDetails.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.ListBox trigger_list;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.GroupBox selectedEntityDetails;
        private System.Windows.Forms.TextBox entityHierarchy;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button selectEntToPointTo;
        private System.Windows.Forms.Button addNewTrigger;
        private System.Windows.Forms.TextBox entityTriggerDelay;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.GroupBox selectedTriggerDetails;
        private System.Windows.Forms.TextBox triggerStartParam;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button deleteParamTrigger;
        private System.Windows.Forms.Button addNewParamTrigger;
        private System.Windows.Forms.Button saveTrigger;
        private System.Windows.Forms.Button moveDown;
        private System.Windows.Forms.Button moveUp;
        private System.Windows.Forms.ListView entity_list;
        private System.Windows.Forms.ColumnHeader funcHeader;
        private System.Windows.Forms.ColumnHeader inheritHeader;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Button button3;
    }
}

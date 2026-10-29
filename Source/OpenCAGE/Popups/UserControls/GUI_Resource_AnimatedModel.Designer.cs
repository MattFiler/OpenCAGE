namespace OpenCAGE.Popups.UserControls
{
    partial class GUI_Resource_AnimatedModel
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.helpersList = new System.Windows.Forms.ListBox();
            this.labelHelpers = new System.Windows.Forms.Label();
            this.mappingsList = new System.Windows.Forms.ListBox();
            this.labelMappings = new System.Windows.Forms.Label();
            this.countsValue = new System.Windows.Forms.Label();
            this.labelCounts = new System.Windows.Forms.Label();
            this.animSetValue = new System.Windows.Forms.Label();
            this.labelAnimSet = new System.Windows.Forms.Label();
            this.skeletonValue = new System.Windows.Forms.Label();
            this.labelSkeleton = new System.Windows.Forms.Label();
            this.idValue = new System.Windows.Forms.Label();
            this.labelId = new System.Windows.Forms.Label();
            this.labelEntry = new System.Windows.Forms.Label();
            this.entryList = new System.Windows.Forms.ComboBox();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.helpersList);
            this.groupBox1.Controls.Add(this.labelHelpers);
            this.groupBox1.Controls.Add(this.mappingsList);
            this.groupBox1.Controls.Add(this.labelMappings);
            this.groupBox1.Controls.Add(this.countsValue);
            this.groupBox1.Controls.Add(this.labelCounts);
            this.groupBox1.Controls.Add(this.animSetValue);
            this.groupBox1.Controls.Add(this.labelAnimSet);
            this.groupBox1.Controls.Add(this.skeletonValue);
            this.groupBox1.Controls.Add(this.labelSkeleton);
            this.groupBox1.Controls.Add(this.idValue);
            this.groupBox1.Controls.Add(this.labelId);
            this.groupBox1.Controls.Add(this.labelEntry);
            this.groupBox1.Controls.Add(this.entryList);
            this.groupBox1.Location = new System.Drawing.Point(3, 3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(832, 250);
            this.groupBox1.TabIndex = 3;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Animated Model";
            // 
            // labelEntry
            // 
            this.labelEntry.AutoSize = true;
            this.labelEntry.Location = new System.Drawing.Point(28, 25);
            this.labelEntry.Name = "labelEntry";
            this.labelEntry.Size = new System.Drawing.Size(40, 13);
            this.labelEntry.TabIndex = 1;
            this.labelEntry.Text = "Entry:";
            // 
            // entryList
            // 
            this.entryList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.entryList.FormattingEnabled = true;
            this.entryList.Location = new System.Drawing.Point(74, 22);
            this.entryList.Name = "entryList";
            this.entryList.Size = new System.Drawing.Size(742, 21);
            this.entryList.TabIndex = 0;
            this.entryList.SelectedIndexChanged += new System.EventHandler(this.animatedModelIndex_SelectedIndexChanged);
            // 
            // labelId
            // 
            this.labelId.AutoSize = true;
            this.labelId.Location = new System.Drawing.Point(47, 54);
            this.labelId.Name = "labelId";
            this.labelId.Size = new System.Drawing.Size(21, 13);
            this.labelId.TabIndex = 2;
            this.labelId.Text = "ID:";
            // 
            // idValue
            // 
            this.idValue.AutoSize = true;
            this.idValue.Location = new System.Drawing.Point(74, 54);
            this.idValue.Name = "idValue";
            this.idValue.Size = new System.Drawing.Size(13, 13);
            this.idValue.TabIndex = 3;
            this.idValue.Text = "0";
            // 
            // labelSkeleton
            // 
            this.labelSkeleton.AutoSize = true;
            this.labelSkeleton.Location = new System.Drawing.Point(160, 54);
            this.labelSkeleton.Name = "labelSkeleton";
            this.labelSkeleton.Size = new System.Drawing.Size(52, 13);
            this.labelSkeleton.TabIndex = 4;
            this.labelSkeleton.Text = "Skeleton:";
            // 
            // skeletonValue
            // 
            this.skeletonValue.AutoSize = true;
            this.skeletonValue.Location = new System.Drawing.Point(218, 54);
            this.skeletonValue.Name = "skeletonValue";
            this.skeletonValue.Size = new System.Drawing.Size(39, 13);
            this.skeletonValue.TabIndex = 5;
            this.skeletonValue.Text = "(none)";
            // 
            // labelAnimSet
            // 
            this.labelAnimSet.AutoSize = true;
            this.labelAnimSet.Location = new System.Drawing.Point(26, 75);
            this.labelAnimSet.Name = "labelAnimSet";
            this.labelAnimSet.Size = new System.Drawing.Size(76, 13);
            this.labelAnimSet.TabIndex = 6;
            this.labelAnimSet.Text = "Animation set:";
            // 
            // animSetValue
            // 
            this.animSetValue.AutoSize = true;
            this.animSetValue.Location = new System.Drawing.Point(108, 75);
            this.animSetValue.Name = "animSetValue";
            this.animSetValue.Size = new System.Drawing.Size(39, 13);
            this.animSetValue.TabIndex = 7;
            this.animSetValue.Text = "(none)";
            // 
            // labelCounts
            // 
            this.labelCounts.AutoSize = true;
            this.labelCounts.Location = new System.Drawing.Point(26, 96);
            this.labelCounts.Name = "labelCounts";
            this.labelCounts.Size = new System.Drawing.Size(42, 13);
            this.labelCounts.TabIndex = 8;
            this.labelCounts.Text = "Counts:";
            // 
            // countsValue
            // 
            this.countsValue.AutoSize = true;
            this.countsValue.Location = new System.Drawing.Point(74, 96);
            this.countsValue.Name = "countsValue";
            this.countsValue.Size = new System.Drawing.Size(39, 13);
            this.countsValue.TabIndex = 9;
            this.countsValue.Text = "(none)";
            // 
            // labelMappings
            // 
            this.labelMappings.AutoSize = true;
            this.labelMappings.Location = new System.Drawing.Point(6, 118);
            this.labelMappings.Name = "labelMappings";
            this.labelMappings.Size = new System.Drawing.Size(112, 13);
            this.labelMappings.TabIndex = 10;
            this.labelMappings.Text = "Bone / mesh mappings:";
            // 
            // mappingsList
            // 
            this.mappingsList.FormattingEnabled = true;
            this.mappingsList.HorizontalScrollbar = true;
            this.mappingsList.Location = new System.Drawing.Point(9, 134);
            this.mappingsList.Name = "mappingsList";
            this.mappingsList.Size = new System.Drawing.Size(400, 108);
            this.mappingsList.TabIndex = 11;
            // 
            // labelHelpers
            // 
            this.labelHelpers.AutoSize = true;
            this.labelHelpers.Location = new System.Drawing.Point(418, 118);
            this.labelHelpers.Name = "labelHelpers";
            this.labelHelpers.Size = new System.Drawing.Size(45, 13);
            this.labelHelpers.TabIndex = 12;
            this.labelHelpers.Text = "Helpers:";
            // 
            // helpersList
            // 
            this.helpersList.FormattingEnabled = true;
            this.helpersList.HorizontalScrollbar = true;
            this.helpersList.Location = new System.Drawing.Point(421, 134);
            this.helpersList.Name = "helpersList";
            this.helpersList.Size = new System.Drawing.Size(395, 108);
            this.helpersList.TabIndex = 13;
            // 
            // GUI_Resource_AnimatedModel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBox1);
            this.Name = "GUI_Resource_AnimatedModel";
            this.Size = new System.Drawing.Size(838, 256);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ComboBox entryList;
        private System.Windows.Forms.Label labelEntry;
        private System.Windows.Forms.Label labelId;
        private System.Windows.Forms.Label idValue;
        private System.Windows.Forms.Label labelSkeleton;
        private System.Windows.Forms.Label skeletonValue;
        private System.Windows.Forms.Label labelAnimSet;
        private System.Windows.Forms.Label animSetValue;
        private System.Windows.Forms.Label labelCounts;
        private System.Windows.Forms.Label countsValue;
        private System.Windows.Forms.Label labelMappings;
        private System.Windows.Forms.ListBox mappingsList;
        private System.Windows.Forms.Label labelHelpers;
        private System.Windows.Forms.ListBox helpersList;
    }
}

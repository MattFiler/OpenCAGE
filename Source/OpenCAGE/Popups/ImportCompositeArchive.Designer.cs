namespace OpenCAGE
{
    partial class ImportCompositeArchive
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
            this.nameLabel = new System.Windows.Forms.Label();
            this.infoButton = new System.Windows.Forms.Button();
            this.descriptionLabel = new System.Windows.Forms.Label();
            this.warningLabel = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.filterBox = new System.Windows.Forms.TextBox();
            this.compositeTree = new System.Windows.Forms.TreeView();
            this.summaryLabel = new System.Windows.Forms.Label();
            this.destinationGroup = new System.Windows.Forms.GroupBox();
            this.levelLabel = new System.Windows.Forms.Label();
            this.levelList = new System.Windows.Forms.CheckedListBox();
            this.allLevelsButton = new System.Windows.Forms.Button();
            this.noLevelsButton = new System.Windows.Forms.Button();
            this.buildAfterImport = new System.Windows.Forms.CheckBox();
            this.overwriteComposites = new System.Windows.Forms.CheckBox();
            this.overwriteAssets = new System.Windows.Forms.CheckBox();
            this.openAfterImport = new System.Windows.Forms.CheckBox();
            this.importButton = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.destinationGroup.SuspendLayout();
            this.SuspendLayout();
            //
            // nameLabel
            //
            this.nameLabel.AutoEllipsis = true;
            this.nameLabel.Location = new System.Drawing.Point(12, 12);
            this.nameLabel.Name = "nameLabel";
            this.nameLabel.Size = new System.Drawing.Size(460, 15);
            this.nameLabel.TabIndex = 0;
            this.nameLabel.Text = "";
            //
            // infoButton
            //
            this.infoButton.Location = new System.Drawing.Point(472, 8);
            this.infoButton.Name = "infoButton";
            this.infoButton.Size = new System.Drawing.Size(100, 23);
            this.infoButton.TabIndex = 13;
            this.infoButton.Text = "More info...";
            this.toolTip1.SetToolTip(this.infoButton, "Where this package came from: the level, the OpenCAGE version and platform that wrote it, and when.");
            this.infoButton.UseVisualStyleBackColor = true;
            this.infoButton.Click += new System.EventHandler(this.infoButton_Click);
            //
            // descriptionLabel
            //
            this.descriptionLabel.AutoEllipsis = true;
            this.descriptionLabel.Location = new System.Drawing.Point(12, 37);
            this.descriptionLabel.Name = "descriptionLabel";
            this.descriptionLabel.Size = new System.Drawing.Size(560, 13);
            this.descriptionLabel.TabIndex = 2;
            this.descriptionLabel.Text = "";
            //
            // warningLabel
            //
            this.warningLabel.AutoEllipsis = true;
            this.warningLabel.ForeColor = System.Drawing.Color.DarkOrange;
            this.warningLabel.Location = new System.Drawing.Point(12, 56);
            this.warningLabel.Name = "warningLabel";
            this.warningLabel.Size = new System.Drawing.Size(560, 13);
            this.warningLabel.TabIndex = 3;
            this.warningLabel.Text = "";
            this.warningLabel.Visible = false;
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 79);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(105, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "Composites (filter):";
            //
            // filterBox
            //
            this.filterBox.Location = new System.Drawing.Point(15, 95);
            this.filterBox.Name = "filterBox";
            this.filterBox.Size = new System.Drawing.Size(557, 20);
            this.filterBox.TabIndex = 5;
            this.toolTip1.SetToolTip(this.filterBox, "Show only composites whose name contains this text.");
            this.filterBox.TextChanged += new System.EventHandler(this.filterBox_TextChanged);
            //
            // compositeTree
            //
            this.compositeTree.CheckBoxes = false;
            this.compositeTree.HideSelection = false;
            this.compositeTree.Location = new System.Drawing.Point(15, 121);
            this.compositeTree.Name = "compositeTree";
            this.compositeTree.Size = new System.Drawing.Size(557, 220);
            this.compositeTree.TabIndex = 6;
            //
            // summaryLabel
            //
            this.summaryLabel.AutoSize = true;
            this.summaryLabel.Location = new System.Drawing.Point(12, 347);
            this.summaryLabel.Name = "summaryLabel";
            this.summaryLabel.Size = new System.Drawing.Size(85, 13);
            this.summaryLabel.TabIndex = 7;
            this.summaryLabel.Text = "Nothing selected";
            //
            // destinationGroup
            //
            this.destinationGroup.Controls.Add(this.levelLabel);
            this.destinationGroup.Controls.Add(this.levelList);
            this.destinationGroup.Controls.Add(this.allLevelsButton);
            this.destinationGroup.Controls.Add(this.noLevelsButton);
            this.destinationGroup.Controls.Add(this.buildAfterImport);
            this.destinationGroup.Location = new System.Drawing.Point(15, 370);
            this.destinationGroup.Name = "destinationGroup";
            this.destinationGroup.Size = new System.Drawing.Size(557, 202);
            this.destinationGroup.TabIndex = 8;
            this.destinationGroup.TabStop = false;
            this.destinationGroup.Text = "Destination (no level is open)";
            //
            // levelLabel
            //
            this.levelLabel.AutoSize = true;
            this.levelLabel.Location = new System.Drawing.Point(12, 20);
            this.levelLabel.Name = "levelLabel";
            this.levelLabel.Size = new System.Drawing.Size(97, 13);
            this.levelLabel.TabIndex = 0;
            this.levelLabel.Text = "Import into levels:";
            //
            // levelList
            //
            this.levelList.CheckOnClick = true;
            this.levelList.FormattingEnabled = true;
            this.levelList.IntegralHeight = false;
            this.levelList.Location = new System.Drawing.Point(15, 36);
            this.levelList.Name = "levelList";
            this.levelList.Size = new System.Drawing.Size(527, 100);
            this.levelList.TabIndex = 1;
            this.toolTip1.SetToolTip(this.levelList, "Tick the levels on disk to import into. Each one is loaded, written to and saved.");
            //
            // allLevelsButton
            //
            this.allLevelsButton.Location = new System.Drawing.Point(15, 142);
            this.allLevelsButton.Name = "allLevelsButton";
            this.allLevelsButton.Size = new System.Drawing.Size(110, 23);
            this.allLevelsButton.TabIndex = 2;
            this.allLevelsButton.Text = "All levels";
            this.toolTip1.SetToolTip(this.allLevelsButton, "Tick every level. Slow: each level is loaded and saved in turn.");
            this.allLevelsButton.UseVisualStyleBackColor = true;
            //
            // noLevelsButton
            //
            this.noLevelsButton.Location = new System.Drawing.Point(131, 142);
            this.noLevelsButton.Name = "noLevelsButton";
            this.noLevelsButton.Size = new System.Drawing.Size(110, 23);
            this.noLevelsButton.TabIndex = 3;
            this.noLevelsButton.Text = "No levels";
            this.noLevelsButton.UseVisualStyleBackColor = true;
            //
            // buildAfterImport
            //
            this.buildAfterImport.AutoSize = true;
            this.buildAfterImport.Location = new System.Drawing.Point(15, 173);
            this.buildAfterImport.Name = "buildAfterImport";
            this.buildAfterImport.Size = new System.Drawing.Size(200, 17);
            this.buildAfterImport.TabIndex = 4;
            this.buildAfterImport.Text = "Build after import (Save and Build)";
            this.toolTip1.SetToolTip(this.buildAfterImport, "Run a full Save and Build on each level instead of a plain save, so the imported geometry gets lighting, collision, navmesh and the rest.");
            this.buildAfterImport.UseVisualStyleBackColor = true;
            //
            // overwriteComposites
            //
            this.overwriteComposites.AutoSize = true;
            this.overwriteComposites.Checked = true;
            this.overwriteComposites.CheckState = System.Windows.Forms.CheckState.Checked;
            this.overwriteComposites.Location = new System.Drawing.Point(15, 582);
            this.overwriteComposites.Name = "overwriteComposites";
            this.overwriteComposites.Size = new System.Drawing.Size(200, 17);
            this.overwriteComposites.TabIndex = 9;
            this.overwriteComposites.Text = "Overwrite composites already here";
            this.toolTip1.SetToolTip(this.overwriteComposites, "If checked: a composite the level already holds under the same ID is replaced by the imported copy. If unchecked, the existing one is kept.");
            this.overwriteComposites.UseVisualStyleBackColor = true;
            //
            // overwriteAssets
            //
            this.overwriteAssets.AutoSize = true;
            this.overwriteAssets.Checked = true;
            this.overwriteAssets.CheckState = System.Windows.Forms.CheckState.Checked;
            this.overwriteAssets.Location = new System.Drawing.Point(15, 605);
            this.overwriteAssets.Name = "overwriteAssets";
            this.overwriteAssets.Size = new System.Drawing.Size(196, 17);
            this.overwriteAssets.TabIndex = 10;
            this.overwriteAssets.Text = "Overwrite existing assets";
            this.toolTip1.SetToolTip(this.overwriteAssets, "If checked: models, textures, materials, and other named assets replace entries with the same name. If unchecked, existing assets with matching names are kept.");
            this.overwriteAssets.UseVisualStyleBackColor = true;
            //
            // openAfterImport
            //
            this.openAfterImport.AutoSize = true;
            this.openAfterImport.Location = new System.Drawing.Point(230, 605);
            this.openAfterImport.Name = "openAfterImport";
            this.openAfterImport.Size = new System.Drawing.Size(196, 17);
            this.openAfterImport.TabIndex = 11;
            this.openAfterImport.Text = "Open composite after import";
            this.toolTip1.SetToolTip(this.openAfterImport, "If checked, the first imported composite will be opened in a new tab automatically.");
            this.openAfterImport.UseVisualStyleBackColor = true;
            this.openAfterImport.Checked = false;
            //
            // importButton
            //
            this.importButton.Location = new System.Drawing.Point(456, 582);
            this.importButton.Name = "importButton";
            this.importButton.Size = new System.Drawing.Size(116, 42);
            this.importButton.TabIndex = 12;
            this.importButton.Text = "Import";
            this.toolTip1.SetToolTip(this.importButton, "Port the package's composites into the level open in the editor. Nothing is written to disk until you save the level.");
            this.importButton.UseVisualStyleBackColor = true;
            this.importButton.Click += new System.EventHandler(this.importButton_Click);
            //
            // ImportCompositeArchive
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 636);
            this.Controls.Add(this.importButton);
            this.Controls.Add(this.openAfterImport);
            this.Controls.Add(this.overwriteAssets);
            this.Controls.Add(this.overwriteComposites);
            this.Controls.Add(this.destinationGroup);
            this.Controls.Add(this.summaryLabel);
            this.Controls.Add(this.compositeTree);
            this.Controls.Add(this.filterBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.warningLabel);
            this.Controls.Add(this.descriptionLabel);
            this.Controls.Add(this.infoButton);
            this.Controls.Add(this.nameLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "ImportCompositeArchive";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Import Composites from Disk";
            this.destinationGroup.ResumeLayout(false);
            this.destinationGroup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label nameLabel;
        private System.Windows.Forms.Button infoButton;
        private System.Windows.Forms.Label descriptionLabel;
        private System.Windows.Forms.Label warningLabel;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox filterBox;
        private System.Windows.Forms.TreeView compositeTree;
        private System.Windows.Forms.Label summaryLabel;
        private System.Windows.Forms.GroupBox destinationGroup;
        private System.Windows.Forms.Label levelLabel;
        private System.Windows.Forms.CheckedListBox levelList;
        private System.Windows.Forms.Button allLevelsButton;
        private System.Windows.Forms.Button noLevelsButton;
        private System.Windows.Forms.CheckBox buildAfterImport;
        private System.Windows.Forms.CheckBox overwriteComposites;
        private System.Windows.Forms.CheckBox overwriteAssets;
        private System.Windows.Forms.CheckBox openAfterImport;
        private System.Windows.Forms.Button importButton;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}

namespace OpenCAGE
{
    partial class ExportComposite
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
            this.export = new System.Windows.Forms.Button();
            this.levelList = new System.Windows.Forms.CheckedListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.allLevelsButton = new System.Windows.Forms.Button();
            this.noLevelsButton = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.filterBox = new System.Windows.Forms.TextBox();
            this.compositeTree = new System.Windows.Forms.TreeView();
            this.checkShown = new System.Windows.Forms.Button();
            this.uncheckShown = new System.Windows.Forms.Button();
            this.summaryLabel = new System.Windows.Forms.Label();
            this.overwrite = new System.Windows.Forms.CheckBox();
            this.overwriteAssets = new System.Windows.Forms.CheckBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.buildAfterPort = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            //
            // export
            //
            this.export.Location = new System.Drawing.Point(324, 511);
            this.export.Name = "export";
            this.export.Size = new System.Drawing.Size(113, 42);
            this.export.TabIndex = 13;
            this.export.Text = "Port Now";
            this.toolTip1.SetToolTip(this.export, "Port the ticked composites to every level ticked above.");
            this.export.UseVisualStyleBackColor = true;
            this.export.Click += new System.EventHandler(this.export_Click);
            //
            // levelList
            //
            this.levelList.CheckOnClick = true;
            this.levelList.FormattingEnabled = true;
            this.levelList.IntegralHeight = false;
            this.levelList.Location = new System.Drawing.Point(15, 29);
            this.levelList.Name = "levelList";
            this.levelList.Size = new System.Drawing.Size(422, 100);
            this.levelList.TabIndex = 1;
            this.toolTip1.SetToolTip(this.levelList, "Tick the levels to port into. The level you have open isn\'t offered.");
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(133, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Port composites to levels:";
            //
            // allLevelsButton
            //
            this.allLevelsButton.Location = new System.Drawing.Point(15, 135);
            this.allLevelsButton.Name = "allLevelsButton";
            this.allLevelsButton.Size = new System.Drawing.Size(110, 23);
            this.allLevelsButton.TabIndex = 2;
            this.allLevelsButton.Text = "All levels";
            this.toolTip1.SetToolTip(this.allLevelsButton, "Tick every other level in the game. Slow: each level is loaded and saved in turn.");
            this.allLevelsButton.UseVisualStyleBackColor = true;
            //
            // noLevelsButton
            //
            this.noLevelsButton.Location = new System.Drawing.Point(131, 135);
            this.noLevelsButton.Name = "noLevelsButton";
            this.noLevelsButton.Size = new System.Drawing.Size(110, 23);
            this.noLevelsButton.TabIndex = 3;
            this.noLevelsButton.Text = "No levels";
            this.noLevelsButton.UseVisualStyleBackColor = true;
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 168);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(105, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "Composites (filter):";
            //
            // filterBox
            //
            this.filterBox.Location = new System.Drawing.Point(15, 184);
            this.filterBox.Name = "filterBox";
            this.filterBox.Size = new System.Drawing.Size(422, 20);
            this.filterBox.TabIndex = 5;
            this.toolTip1.SetToolTip(this.filterBox, "Show only composites whose name contains this text.");
            this.filterBox.TextChanged += new System.EventHandler(this.filterBox_TextChanged);
            //
            // compositeTree
            //
            this.compositeTree.CheckBoxes = true;
            this.compositeTree.HideSelection = false;
            this.compositeTree.Location = new System.Drawing.Point(15, 210);
            this.compositeTree.Name = "compositeTree";
            this.compositeTree.Size = new System.Drawing.Size(422, 220);
            this.compositeTree.TabIndex = 6;
            //
            // checkShown
            //
            this.checkShown.Location = new System.Drawing.Point(15, 436);
            this.checkShown.Name = "checkShown";
            this.checkShown.Size = new System.Drawing.Size(110, 23);
            this.checkShown.TabIndex = 7;
            this.checkShown.Text = "Check all";
            this.toolTip1.SetToolTip(this.checkShown, "Tick every composite the tree is showing - with a filter typed, just the matches.");
            this.checkShown.UseVisualStyleBackColor = true;
            this.checkShown.Click += new System.EventHandler(this.checkShown_Click);
            //
            // uncheckShown
            //
            this.uncheckShown.Location = new System.Drawing.Point(131, 436);
            this.uncheckShown.Name = "uncheckShown";
            this.uncheckShown.Size = new System.Drawing.Size(110, 23);
            this.uncheckShown.TabIndex = 8;
            this.uncheckShown.Text = "Uncheck all";
            this.toolTip1.SetToolTip(this.uncheckShown, "Untick every composite, shown by the filter or not.");
            this.uncheckShown.UseVisualStyleBackColor = true;
            this.uncheckShown.Click += new System.EventHandler(this.uncheckShown_Click);
            //
            // summaryLabel
            //
            this.summaryLabel.AutoSize = true;
            this.summaryLabel.Location = new System.Drawing.Point(12, 466);
            this.summaryLabel.Name = "summaryLabel";
            this.summaryLabel.Size = new System.Drawing.Size(85, 13);
            this.summaryLabel.TabIndex = 9;
            this.summaryLabel.Text = "Nothing selected";
            //
            // overwrite
            //
            this.overwrite.AutoSize = true;
            this.overwrite.Checked = true;
            this.overwrite.CheckState = System.Windows.Forms.CheckState.Checked;
            this.overwrite.Location = new System.Drawing.Point(15, 488);
            this.overwrite.Name = "overwrite";
            this.overwrite.Size = new System.Drawing.Size(219, 17);
            this.overwrite.TabIndex = 10;
            this.overwrite.Text = "Overwrite existing destination composites";
            this.toolTip1.SetToolTip(this.overwrite, "If checked: when composites are copied they will overwrite any by the same ID in " +
        "the destination level.");
            this.overwrite.UseVisualStyleBackColor = true;
            //
            // overwriteAssets
            //
            this.overwriteAssets.AutoSize = true;
            this.overwriteAssets.Location = new System.Drawing.Point(15, 511);
            this.overwriteAssets.Name = "overwriteAssets";
            this.overwriteAssets.Size = new System.Drawing.Size(196, 17);
            this.overwriteAssets.TabIndex = 11;
            this.overwriteAssets.Text = "Overwrite existing destination assets";
            this.toolTip1.SetToolTip(this.overwriteAssets, "If checked: models, textures, materials, and other named assets replace destinati" +
        "on entries with the same name. If unchecked, existing assets with matching names" +
        " are kept.");
            this.overwriteAssets.UseVisualStyleBackColor = true;
            //
            // buildAfterPort
            //
            this.buildAfterPort.AutoSize = true;
            this.buildAfterPort.Location = new System.Drawing.Point(324, 488);
            this.buildAfterPort.Name = "buildAfterPort";
            this.buildAfterPort.Size = new System.Drawing.Size(180, 17);
            this.buildAfterPort.TabIndex = 12;
            this.buildAfterPort.Text = "Build after port (Save and Build)";
            this.toolTip1.SetToolTip(this.buildAfterPort, "Run a full Save and Build on each destination instead of a plain save, so the ported geometry gets lighting, collision, navmesh and the rest. Slow; keep this off if you intend to build later.");
            this.buildAfterPort.UseVisualStyleBackColor = true;
            //
            // ExportComposite
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(449, 565);
            this.Controls.Add(this.buildAfterPort);
            this.Controls.Add(this.overwriteAssets);
            this.Controls.Add(this.overwrite);
            this.Controls.Add(this.summaryLabel);
            this.Controls.Add(this.uncheckShown);
            this.Controls.Add(this.checkShown);
            this.Controls.Add(this.compositeTree);
            this.Controls.Add(this.filterBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.noLevelsButton);
            this.Controls.Add(this.allLevelsButton);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.levelList);
            this.Controls.Add(this.export);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "ExportComposite";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Port Composites";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button export;
        private System.Windows.Forms.CheckedListBox levelList;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button allLevelsButton;
        private System.Windows.Forms.Button noLevelsButton;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox filterBox;
        private System.Windows.Forms.TreeView compositeTree;
        private System.Windows.Forms.Button checkShown;
        private System.Windows.Forms.Button uncheckShown;
        private System.Windows.Forms.Label summaryLabel;
        private System.Windows.Forms.CheckBox overwrite;
        private System.Windows.Forms.CheckBox overwriteAssets;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.CheckBox buildAfterPort;
    }
}

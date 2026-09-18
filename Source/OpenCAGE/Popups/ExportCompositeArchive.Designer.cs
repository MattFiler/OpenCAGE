namespace OpenCAGE
{
    partial class ExportCompositeArchive
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
            this.nameLabel = new System.Windows.Forms.Label();
            this.nameBox = new System.Windows.Forms.TextBox();
            this.descriptionLabel = new System.Windows.Forms.Label();
            this.descriptionBox = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.filterBox = new System.Windows.Forms.TextBox();
            this.compositeTree = new System.Windows.Forms.TreeView();
            this.checkShown = new System.Windows.Forms.Button();
            this.uncheckShown = new System.Windows.Forms.Button();
            this.summaryLabel = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            //
            // export
            //
            this.export.Location = new System.Drawing.Point(324, 428);
            this.export.Name = "export";
            this.export.Size = new System.Drawing.Size(113, 42);
            this.export.TabIndex = 11;
            this.export.Text = "Export to Disk...";
            this.toolTip1.SetToolTip(this.export, "Write the ticked composites, with the models, materials, textures, collision, physics and animations they use, to an .ocp package.");
            this.export.UseVisualStyleBackColor = true;
            this.export.Click += new System.EventHandler(this.export_Click);
            //
            // nameLabel
            //
            this.nameLabel.AutoSize = true;
            this.nameLabel.Location = new System.Drawing.Point(12, 13);
            this.nameLabel.Name = "nameLabel";
            this.nameLabel.Size = new System.Drawing.Size(38, 13);
            this.nameLabel.TabIndex = 0;
            this.nameLabel.Text = "Name:";
            //
            // nameBox
            //
            this.nameBox.Location = new System.Drawing.Point(15, 29);
            this.nameBox.Name = "nameBox";
            this.nameBox.Size = new System.Drawing.Size(422, 20);
            this.nameBox.TabIndex = 1;
            this.toolTip1.SetToolTip(this.nameBox, "What the package is called - shown to whoever imports it. Left blank, the file name is used.");
            //
            // descriptionLabel
            //
            this.descriptionLabel.AutoSize = true;
            this.descriptionLabel.Location = new System.Drawing.Point(12, 55);
            this.descriptionLabel.Name = "descriptionLabel";
            this.descriptionLabel.Size = new System.Drawing.Size(63, 13);
            this.descriptionLabel.TabIndex = 4;
            this.descriptionLabel.Text = "Description:";
            //
            // descriptionBox
            //
            this.descriptionBox.AcceptsReturn = true;
            this.descriptionBox.Location = new System.Drawing.Point(15, 71);
            this.descriptionBox.Multiline = true;
            this.descriptionBox.Name = "descriptionBox";
            this.descriptionBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.descriptionBox.Size = new System.Drawing.Size(422, 46);
            this.descriptionBox.TabIndex = 5;
            this.toolTip1.SetToolTip(this.descriptionBox, "What is in the package and how to use it - shown when it is imported.");
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 127);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(105, 13);
            this.label2.TabIndex = 6;
            this.label2.Text = "Composites (filter):";
            //
            // filterBox
            //
            this.filterBox.Location = new System.Drawing.Point(15, 143);
            this.filterBox.Name = "filterBox";
            this.filterBox.Size = new System.Drawing.Size(422, 20);
            this.filterBox.TabIndex = 7;
            this.toolTip1.SetToolTip(this.filterBox, "Show only composites whose name contains this text.");
            this.filterBox.TextChanged += new System.EventHandler(this.filterBox_TextChanged);
            //
            // compositeTree
            //
            this.compositeTree.CheckBoxes = true;
            this.compositeTree.HideSelection = false;
            this.compositeTree.Location = new System.Drawing.Point(15, 169);
            this.compositeTree.Name = "compositeTree";
            this.compositeTree.Size = new System.Drawing.Size(422, 200);
            this.compositeTree.TabIndex = 8;
            //
            // checkShown
            //
            this.checkShown.Location = new System.Drawing.Point(15, 375);
            this.checkShown.Name = "checkShown";
            this.checkShown.Size = new System.Drawing.Size(110, 23);
            this.checkShown.TabIndex = 9;
            this.checkShown.Text = "Check all";
            this.toolTip1.SetToolTip(this.checkShown, "Tick every composite the tree is showing - with a filter typed, just the matches.");
            this.checkShown.UseVisualStyleBackColor = true;
            this.checkShown.Click += new System.EventHandler(this.checkShown_Click);
            //
            // uncheckShown
            //
            this.uncheckShown.Location = new System.Drawing.Point(131, 375);
            this.uncheckShown.Name = "uncheckShown";
            this.uncheckShown.Size = new System.Drawing.Size(110, 23);
            this.uncheckShown.TabIndex = 10;
            this.uncheckShown.Text = "Uncheck all";
            this.toolTip1.SetToolTip(this.uncheckShown, "Untick every composite, shown by the filter or not.");
            this.uncheckShown.UseVisualStyleBackColor = true;
            this.uncheckShown.Click += new System.EventHandler(this.uncheckShown_Click);
            //
            // summaryLabel
            //
            this.summaryLabel.AutoSize = true;
            this.summaryLabel.Location = new System.Drawing.Point(12, 406);
            this.summaryLabel.Name = "summaryLabel";
            this.summaryLabel.Size = new System.Drawing.Size(85, 13);
            this.summaryLabel.TabIndex = 12;
            this.summaryLabel.Text = "Nothing selected";
            //
            // ExportCompositeArchive
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(449, 482);
            this.Controls.Add(this.summaryLabel);
            this.Controls.Add(this.uncheckShown);
            this.Controls.Add(this.checkShown);
            this.Controls.Add(this.compositeTree);
            this.Controls.Add(this.filterBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.descriptionBox);
            this.Controls.Add(this.descriptionLabel);
            this.Controls.Add(this.nameBox);
            this.Controls.Add(this.nameLabel);
            this.Controls.Add(this.export);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "ExportCompositeArchive";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Export Composites to Disk";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button export;
        private System.Windows.Forms.Label nameLabel;
        private System.Windows.Forms.TextBox nameBox;
        private System.Windows.Forms.Label descriptionLabel;
        private System.Windows.Forms.TextBox descriptionBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox filterBox;
        private System.Windows.Forms.TreeView compositeTree;
        private System.Windows.Forms.Button checkShown;
        private System.Windows.Forms.Button uncheckShown;
        private System.Windows.Forms.Label summaryLabel;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}

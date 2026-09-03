namespace OpenCAGE
{
    partial class ImportComposites
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
            this.label1 = new System.Windows.Forms.Label();
            this.levelList = new System.Windows.Forms.ListBox();
            this.label2 = new System.Windows.Forms.Label();
            this.filterBox = new System.Windows.Forms.TextBox();
            this.compositeList = new System.Windows.Forms.CheckedListBox();
            this.checkShown = new System.Windows.Forms.Button();
            this.uncheckShown = new System.Windows.Forms.Button();
            this.includeChildren = new System.Windows.Forms.CheckBox();
            this.overwriteComposites = new System.Windows.Forms.CheckBox();
            this.overwriteAssets = new System.Windows.Forms.CheckBox();
            this.summaryLabel = new System.Windows.Forms.Label();
            this.importButton = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(36, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Level:";
            //
            // levelList
            //
            this.levelList.FormattingEnabled = true;
            this.levelList.IntegralHeight = false;
            this.levelList.Location = new System.Drawing.Point(15, 28);
            this.levelList.Name = "levelList";
            this.levelList.Size = new System.Drawing.Size(217, 375);
            this.levelList.TabIndex = 1;
            this.levelList.SelectedIndexChanged += new System.EventHandler(this.levelList_SelectedIndexChanged);
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(244, 12);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(105, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Composites (filter):";
            //
            // filterBox
            //
            this.filterBox.Location = new System.Drawing.Point(247, 28);
            this.filterBox.Name = "filterBox";
            this.filterBox.Size = new System.Drawing.Size(501, 20);
            this.filterBox.TabIndex = 3;
            this.toolTip1.SetToolTip(this.filterBox, "Show only composites whose name contains this text.");
            this.filterBox.TextChanged += new System.EventHandler(this.filterBox_TextChanged);
            //
            // compositeList
            //
            this.compositeList.CheckOnClick = true;
            this.compositeList.FormattingEnabled = true;
            this.compositeList.IntegralHeight = false;
            this.compositeList.Location = new System.Drawing.Point(247, 54);
            this.compositeList.Name = "compositeList";
            this.compositeList.Size = new System.Drawing.Size(501, 320);
            this.compositeList.TabIndex = 4;
            this.compositeList.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.compositeList_ItemCheck);
            //
            // checkShown
            //
            this.checkShown.Location = new System.Drawing.Point(247, 380);
            this.checkShown.Name = "checkShown";
            this.checkShown.Size = new System.Drawing.Size(120, 23);
            this.checkShown.TabIndex = 5;
            this.checkShown.Text = "Check all shown";
            this.checkShown.UseVisualStyleBackColor = true;
            this.checkShown.Click += new System.EventHandler(this.checkShown_Click);
            //
            // uncheckShown
            //
            this.uncheckShown.Location = new System.Drawing.Point(373, 380);
            this.uncheckShown.Name = "uncheckShown";
            this.uncheckShown.Size = new System.Drawing.Size(120, 23);
            this.uncheckShown.TabIndex = 6;
            this.uncheckShown.Text = "Uncheck all shown";
            this.uncheckShown.UseVisualStyleBackColor = true;
            this.uncheckShown.Click += new System.EventHandler(this.uncheckShown_Click);
            //
            // includeChildren
            //
            this.includeChildren.AutoSize = true;
            this.includeChildren.Checked = true;
            this.includeChildren.CheckState = System.Windows.Forms.CheckState.Checked;
            this.includeChildren.Location = new System.Drawing.Point(15, 412);
            this.includeChildren.Name = "includeChildren";
            this.includeChildren.Size = new System.Drawing.Size(218, 17);
            this.includeChildren.TabIndex = 7;
            this.includeChildren.Text = "Also bring composites they instance";
            this.toolTip1.SetToolTip(this.includeChildren, "If checked: composites instanced within the chosen composites are copied too, all the way down.");
            this.includeChildren.UseVisualStyleBackColor = true;
            //
            // overwriteComposites
            //
            this.overwriteComposites.AutoSize = true;
            this.overwriteComposites.Location = new System.Drawing.Point(15, 435);
            this.overwriteComposites.Name = "overwriteComposites";
            this.overwriteComposites.Size = new System.Drawing.Size(200, 17);
            this.overwriteComposites.TabIndex = 8;
            this.overwriteComposites.Text = "Overwrite composites already here";
            this.toolTip1.SetToolTip(this.overwriteComposites, "If checked: a composite this level already holds under the same ID is replaced by the imported copy. If unchecked, the existing one is kept.");
            this.overwriteComposites.UseVisualStyleBackColor = true;
            //
            // overwriteAssets
            //
            this.overwriteAssets.AutoSize = true;
            this.overwriteAssets.Location = new System.Drawing.Point(247, 412);
            this.overwriteAssets.Name = "overwriteAssets";
            this.overwriteAssets.Size = new System.Drawing.Size(196, 17);
            this.overwriteAssets.TabIndex = 9;
            this.overwriteAssets.Text = "Overwrite existing assets";
            this.toolTip1.SetToolTip(this.overwriteAssets, "If checked: models, textures, materials, and other named assets replace entries with the same name. If unchecked, existing assets with matching names are kept.");
            this.overwriteAssets.UseVisualStyleBackColor = true;
            //
            // summaryLabel
            //
            this.summaryLabel.AutoSize = true;
            this.summaryLabel.Location = new System.Drawing.Point(244, 438);
            this.summaryLabel.Name = "summaryLabel";
            this.summaryLabel.Size = new System.Drawing.Size(85, 13);
            this.summaryLabel.TabIndex = 10;
            this.summaryLabel.Text = "Nothing selected";
            //
            // importButton
            //
            this.importButton.Location = new System.Drawing.Point(632, 410);
            this.importButton.Name = "importButton";
            this.importButton.Size = new System.Drawing.Size(116, 42);
            this.importButton.TabIndex = 11;
            this.importButton.Text = "Import";
            this.importButton.UseVisualStyleBackColor = true;
            this.importButton.Click += new System.EventHandler(this.importButton_Click);
            //
            // ImportComposites
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(760, 464);
            this.Controls.Add(this.importButton);
            this.Controls.Add(this.summaryLabel);
            this.Controls.Add(this.overwriteAssets);
            this.Controls.Add(this.overwriteComposites);
            this.Controls.Add(this.includeChildren);
            this.Controls.Add(this.uncheckShown);
            this.Controls.Add(this.checkShown);
            this.Controls.Add(this.compositeList);
            this.Controls.Add(this.filterBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.levelList);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "ImportComposites";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Import Composites";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ListBox levelList;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox filterBox;
        private System.Windows.Forms.CheckedListBox compositeList;
        private System.Windows.Forms.Button checkShown;
        private System.Windows.Forms.Button uncheckShown;
        private System.Windows.Forms.CheckBox includeChildren;
        private System.Windows.Forms.CheckBox overwriteComposites;
        private System.Windows.Forms.CheckBox overwriteAssets;
        private System.Windows.Forms.Label summaryLabel;
        private System.Windows.Forms.Button importButton;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}

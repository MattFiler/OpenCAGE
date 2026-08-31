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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ExportComposite));
            this.export = new System.Windows.Forms.Button();
            this.levelList = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.portToAllLevels = new System.Windows.Forms.CheckBox();
            this.overwrite = new System.Windows.Forms.CheckBox();
            this.overwriteAssets = new System.Windows.Forms.CheckBox();
            this.recurse = new System.Windows.Forms.CheckBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.buildAfterPort = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // export
            // 
            this.export.Location = new System.Drawing.Point(324, 89);
            this.export.Name = "export";
            this.export.Size = new System.Drawing.Size(113, 42);
            this.export.TabIndex = 10;
            this.export.Text = "Port Now";
            this.toolTip1.SetToolTip(this.export, "Export composite to the selected level, or every level if \"Port to all levels\" is" +
        " checked.");
            this.export.UseVisualStyleBackColor = true;
            this.export.Click += new System.EventHandler(this.export_Click);
            // 
            // levelList
            // 
            this.levelList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.levelList.FormattingEnabled = true;
            this.levelList.Location = new System.Drawing.Point(15, 29);
            this.levelList.Name = "levelList";
            this.levelList.Size = new System.Drawing.Size(422, 21);
            this.levelList.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(117, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Port composite to level:";
            // 
            // portToAllLevels
            // 
            this.portToAllLevels.AutoSize = true;
            this.portToAllLevels.Location = new System.Drawing.Point(337, 9);
            this.portToAllLevels.Name = "portToAllLevels";
            this.portToAllLevels.Size = new System.Drawing.Size(100, 17);
            this.portToAllLevels.TabIndex = 2;
            this.portToAllLevels.Text = "Port to all levels";
            this.toolTip1.SetToolTip(this.portToAllLevels, "If checked: port the composite to every level except the currently loaded one.");
            this.portToAllLevels.UseVisualStyleBackColor = true;
            this.portToAllLevels.CheckedChanged += new System.EventHandler(this.portToAllLevels_CheckedChanged);
            // 
            // overwrite
            // 
            this.overwrite.AutoSize = true;
            this.overwrite.Checked = true;
            this.overwrite.CheckState = System.Windows.Forms.CheckState.Checked;
            this.overwrite.Location = new System.Drawing.Point(15, 66);
            this.overwrite.Name = "overwrite";
            this.overwrite.Size = new System.Drawing.Size(219, 17);
            this.overwrite.TabIndex = 3;
            this.overwrite.Text = "Overwrite existing destination composites";
            this.toolTip1.SetToolTip(this.overwrite, "If checked: when composites are copied they will overwrite any by the same ID in " +
        "the destination level.");
            this.overwrite.UseVisualStyleBackColor = true;
            // 
            // overwriteAssets
            // 
            this.overwriteAssets.AutoSize = true;
            this.overwriteAssets.Location = new System.Drawing.Point(15, 89);
            this.overwriteAssets.Name = "overwriteAssets";
            this.overwriteAssets.Size = new System.Drawing.Size(196, 17);
            this.overwriteAssets.TabIndex = 4;
            this.overwriteAssets.Text = "Overwrite existing destination assets";
            this.toolTip1.SetToolTip(this.overwriteAssets, "If checked: models, textures, materials, and other named assets replace destinati" +
        "on entries with the same name. If unchecked, existing assets with matching names" +
        " are kept.");
            this.overwriteAssets.UseVisualStyleBackColor = true;
            // 
            // recurse
            // 
            this.recurse.AutoSize = true;
            this.recurse.Checked = true;
            this.recurse.CheckState = System.Windows.Forms.CheckState.Checked;
            this.recurse.Location = new System.Drawing.Point(15, 112);
            this.recurse.Name = "recurse";
            this.recurse.Size = new System.Drawing.Size(257, 17);
            this.recurse.TabIndex = 5;
            this.recurse.Text = "Copy all composites referenced by this composite";
            this.toolTip1.SetToolTip(this.recurse, "If checked: composites that are instanced within the exported composite will also" +
        " be copied.");
            this.recurse.UseVisualStyleBackColor = true;
            // 
            // buildAfterPort
            // 
            this.buildAfterPort.AutoSize = true;
            this.buildAfterPort.Location = new System.Drawing.Point(324, 66);
            this.buildAfterPort.Name = "buildAfterPort";
            this.buildAfterPort.Size = new System.Drawing.Size(96, 17);
            this.buildAfterPort.TabIndex = 11;
            this.buildAfterPort.Text = "Build After Port";
            this.toolTip1.SetToolTip(this.buildAfterPort, "If checked: the destination level(s) will fully rebuild after porting the content" +
        ". Will fix up instanced objects, but take some time. Keep this off if you intend" +
        " to manually build later.");
            this.buildAfterPort.UseVisualStyleBackColor = true;
            // 
            // ExportComposite
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(449, 148);
            this.Controls.Add(this.buildAfterPort);
            this.Controls.Add(this.recurse);
            this.Controls.Add(this.overwriteAssets);
            this.Controls.Add(this.overwrite);
            this.Controls.Add(this.portToAllLevels);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.levelList);
            this.Controls.Add(this.export);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "ExportComposite";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Port Composite";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button export;
        private System.Windows.Forms.ComboBox levelList;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox portToAllLevels;
        private System.Windows.Forms.CheckBox overwrite;
        private System.Windows.Forms.CheckBox overwriteAssets;
        private System.Windows.Forms.CheckBox recurse;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.CheckBox buildAfterPort;
    }
}

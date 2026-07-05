namespace OpenCAGE.Popups.UserControls
{
    partial class GameDirectory
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.setAsDefault = new System.Windows.Forms.Button();
            this.openInEditor = new System.Windows.Forms.Button();
            this.gameInstallDir = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.gameVersion = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // setAsDefault
            // 
            this.setAsDefault.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.setAsDefault.Location = new System.Drawing.Point(601, 19);
            this.setAsDefault.Name = "setAsDefault";
            this.setAsDefault.Size = new System.Drawing.Size(121, 23);
            this.setAsDefault.TabIndex = 1;
            this.setAsDefault.Text = "Set As Default";
            this.toolTip1.SetToolTip(this.setAsDefault, "The default game install will be the one OpenCAGE launches to when started up.");
            this.setAsDefault.UseVisualStyleBackColor = true;
            this.setAsDefault.Click += new System.EventHandler(this.setAsDefault_Click);
            // 
            // openInEditor
            // 
            this.openInEditor.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.openInEditor.Location = new System.Drawing.Point(728, 19);
            this.openInEditor.Name = "openInEditor";
            this.openInEditor.Size = new System.Drawing.Size(121, 23);
            this.openInEditor.TabIndex = 2;
            this.openInEditor.Text = "Open Editor";
            this.toolTip1.SetToolTip(this.openInEditor, "Open a new instance of OpenCAGE now using this game install.");
            this.openInEditor.UseVisualStyleBackColor = true;
            this.openInEditor.Click += new System.EventHandler(this.openInEditor_Click);
            // 
            // gameInstallDir
            // 
            this.gameInstallDir.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gameInstallDir.Location = new System.Drawing.Point(13, 21);
            this.gameInstallDir.Name = "gameInstallDir";
            this.gameInstallDir.ReadOnly = true;
            this.gameInstallDir.Size = new System.Drawing.Size(582, 20);
            this.gameInstallDir.TabIndex = 3;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(10, 44);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(126, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "Detected Game Version: ";
            // 
            // gameVersion
            // 
            this.gameVersion.AutoSize = true;
            this.gameVersion.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.gameVersion.Location = new System.Drawing.Point(133, 44);
            this.gameVersion.Name = "gameVersion";
            this.gameVersion.Size = new System.Drawing.Size(49, 13);
            this.gameVersion.TabIndex = 5;
            this.gameVersion.Text = "STEAM";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.gameInstallDir);
            this.groupBox1.Controls.Add(this.gameVersion);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.setAsDefault);
            this.groupBox1.Controls.Add(this.openInEditor);
            this.groupBox1.Location = new System.Drawing.Point(3, 3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(855, 72);
            this.groupBox1.TabIndex = 6;
            this.groupBox1.TabStop = false;
            // 
            // GameDirectory
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBox1);
            this.Name = "GameDirectory";
            this.Size = new System.Drawing.Size(861, 83);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button setAsDefault;
        private System.Windows.Forms.Button openInEditor;
        private System.Windows.Forms.TextBox gameInstallDir;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label gameVersion;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}

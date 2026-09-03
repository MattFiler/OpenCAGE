namespace OpenCAGE
{
    partial class CreateLevel
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
            this.createLevel = new System.Windows.Forms.Button();
            this.levelName = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.importSummary = new System.Windows.Forms.ListBox();
            this.chooseImports = new System.Windows.Forms.Button();
            this.clearImports = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            //
            // createLevel
            //
            this.createLevel.Location = new System.Drawing.Point(324, 232);
            this.createLevel.Name = "createLevel";
            this.createLevel.Size = new System.Drawing.Size(113, 42);
            this.createLevel.TabIndex = 5;
            this.createLevel.Text = "Create Level";
            this.toolTip1.SetToolTip(this.createLevel, "Create the level, import any chosen composites, and open it in the editor.");
            this.createLevel.UseVisualStyleBackColor = true;
            this.createLevel.Click += new System.EventHandler(this.createLevel_Click);
            //
            // levelName
            //
            this.levelName.Location = new System.Drawing.Point(15, 29);
            this.levelName.Name = "levelName";
            this.levelName.Size = new System.Drawing.Size(422, 20);
            this.levelName.TabIndex = 1;
            this.toolTip1.SetToolTip(this.levelName, "Letters, digits and underscores. The level is created at DATA\\ENV\\<name>, starting from the s" +
        "hared data every level carries.");
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(64, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Level name:";
            //
            // label3
            //
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 58);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(180, 13);
            this.label3.TabIndex = 2;
            this.label3.Text = "Composites to import (optional):";
            //
            // importSummary
            //
            this.importSummary.FormattingEnabled = true;
            this.importSummary.IntegralHeight = false;
            this.importSummary.Location = new System.Drawing.Point(15, 74);
            this.importSummary.Name = "importSummary";
            this.importSummary.SelectionMode = System.Windows.Forms.SelectionMode.None;
            this.importSummary.Size = new System.Drawing.Size(422, 120);
            this.importSummary.TabIndex = 3;
            this.toolTip1.SetToolTip(this.importSummary, "Composites (and the models, materials, textures, collision and physics they use) that will be por" +
        "ted into the new level. Nothing is placed in the world - the content is there to build with. GLOB" +
        "AL, PAUSEMENU and the required assets always come along.");
            //
            // chooseImports
            //
            this.chooseImports.Location = new System.Drawing.Point(15, 200);
            this.chooseImports.Name = "chooseImports";
            this.chooseImports.Size = new System.Drawing.Size(120, 23);
            this.chooseImports.TabIndex = 4;
            this.chooseImports.Text = "Choose...";
            this.toolTip1.SetToolTip(this.chooseImports, "Browse every level and tick the composites to bring into the new level.");
            this.chooseImports.UseVisualStyleBackColor = true;
            this.chooseImports.Click += new System.EventHandler(this.chooseImports_Click);
            //
            // clearImports
            //
            this.clearImports.Location = new System.Drawing.Point(141, 200);
            this.clearImports.Name = "clearImports";
            this.clearImports.Size = new System.Drawing.Size(80, 23);
            this.clearImports.TabIndex = 6;
            this.clearImports.Text = "Clear";
            this.clearImports.UseVisualStyleBackColor = true;
            this.clearImports.Click += new System.EventHandler(this.clearImports_Click);
            //
            // CreateLevel
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(449, 286);
            this.Controls.Add(this.clearImports);
            this.Controls.Add(this.chooseImports);
            this.Controls.Add(this.importSummary);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.levelName);
            this.Controls.Add(this.createLevel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "CreateLevel";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Create Level";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button createLevel;
        private System.Windows.Forms.TextBox levelName;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ListBox importSummary;
        private System.Windows.Forms.Button chooseImports;
        private System.Windows.Forms.Button clearImports;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}

namespace OpenCAGE
{
    partial class ImportAnimation
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.fromLabel = new System.Windows.Forms.Label();
            this.fileLabel = new System.Windows.Forms.Label();
            this.nameLabel = new System.Windows.Forms.Label();
            this.nameBox = new System.Windows.Forms.TextBox();
            this.pathLabel = new System.Windows.Forms.Label();
            this.pathBox = new System.Windows.Forms.TextBox();
            this.rigLabel = new System.Windows.Forms.Label();
            this.rigBox = new System.Windows.Forms.ComboBox();
            this.rootLabel = new System.Windows.Forms.Label();
            this.rootBox = new System.Windows.Forms.ComboBox();
            this.rateLabel = new System.Windows.Forms.Label();
            this.rateBox = new System.Windows.Forms.ComboBox();
            this.additiveCheck = new System.Windows.Forms.CheckBox();
            this.summaryBox = new System.Windows.Forms.TextBox();
            this.previewBtn = new System.Windows.Forms.Button();
            this.importBtn = new System.Windows.Forms.Button();
            this.cancelBtn = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // fromLabel
            //
            this.fromLabel.AutoSize = true;
            this.fromLabel.Location = new System.Drawing.Point(12, 15);
            this.fromLabel.Name = "fromLabel";
            this.fromLabel.Size = new System.Drawing.Size(30, 13);
            this.fromLabel.Text = "From";
            //
            // fileLabel
            //
            this.fileLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                | System.Windows.Forms.AnchorStyles.Right)));
            this.fileLabel.AutoEllipsis = true;
            this.fileLabel.Location = new System.Drawing.Point(120, 15);
            this.fileLabel.Name = "fileLabel";
            this.fileLabel.Size = new System.Drawing.Size(432, 15);
            this.fileLabel.Text = "-";
            //
            // nameLabel
            //
            this.nameLabel.AutoSize = true;
            this.nameLabel.Location = new System.Drawing.Point(12, 46);
            this.nameLabel.Name = "nameLabel";
            this.nameLabel.Size = new System.Drawing.Size(75, 13);
            this.nameLabel.Text = "Play it by";
            //
            // nameBox
            //
            this.nameBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                | System.Windows.Forms.AnchorStyles.Right)));
            this.nameBox.Location = new System.Drawing.Point(120, 43);
            this.nameBox.Name = "nameBox";
            this.nameBox.Size = new System.Drawing.Size(432, 20);
            this.nameBox.TabIndex = 0;
            //
            // pathLabel
            //
            this.pathLabel.AutoSize = true;
            this.pathLabel.Location = new System.Drawing.Point(12, 75);
            this.pathLabel.Name = "pathLabel";
            this.pathLabel.Size = new System.Drawing.Size(56, 13);
            this.pathLabel.Text = "Stored as";
            //
            // pathBox
            //
            this.pathBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                | System.Windows.Forms.AnchorStyles.Right)));
            this.pathBox.Location = new System.Drawing.Point(120, 72);
            this.pathBox.Name = "pathBox";
            this.pathBox.Size = new System.Drawing.Size(432, 20);
            this.pathBox.TabIndex = 1;
            //
            // rigLabel
            //
            this.rigLabel.AutoSize = true;
            this.rigLabel.Location = new System.Drawing.Point(12, 106);
            this.rigLabel.Name = "rigLabel";
            this.rigLabel.Size = new System.Drawing.Size(24, 13);
            this.rigLabel.Text = "Rig";
            //
            // rigBox
            //
            this.rigBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                | System.Windows.Forms.AnchorStyles.Right)));
            this.rigBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.rigBox.Location = new System.Drawing.Point(120, 102);
            this.rigBox.Name = "rigBox";
            this.rigBox.Size = new System.Drawing.Size(432, 21);
            this.rigBox.TabIndex = 2;
            //
            // rootLabel
            //
            this.rootLabel.AutoSize = true;
            this.rootLabel.Location = new System.Drawing.Point(12, 137);
            this.rootLabel.Name = "rootLabel";
            this.rootLabel.Size = new System.Drawing.Size(57, 13);
            this.rootLabel.Text = "Root bone";
            //
            // rootBox
            //
            this.rootBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                | System.Windows.Forms.AnchorStyles.Right)));
            this.rootBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.rootBox.Location = new System.Drawing.Point(120, 133);
            this.rootBox.Name = "rootBox";
            this.rootBox.Size = new System.Drawing.Size(432, 21);
            this.rootBox.TabIndex = 3;
            //
            // rateLabel
            //
            this.rateLabel.AutoSize = true;
            this.rateLabel.Location = new System.Drawing.Point(12, 168);
            this.rateLabel.Name = "rateLabel";
            this.rateLabel.Size = new System.Drawing.Size(60, 13);
            this.rateLabel.Text = "Frame rate";
            //
            // rateBox
            //
            this.rateBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.rateBox.Location = new System.Drawing.Point(120, 164);
            this.rateBox.Name = "rateBox";
            this.rateBox.Size = new System.Drawing.Size(200, 21);
            this.rateBox.TabIndex = 4;
            //
            // additiveCheck
            //
            this.additiveCheck.AutoSize = true;
            this.additiveCheck.Location = new System.Drawing.Point(340, 166);
            this.additiveCheck.Name = "additiveCheck";
            this.additiveCheck.Size = new System.Drawing.Size(180, 17);
            this.additiveCheck.TabIndex = 5;
            this.additiveCheck.Text = "Layer over what is already playing";
            this.additiveCheck.UseVisualStyleBackColor = true;
            //
            // summaryBox
            //
            this.summaryBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.summaryBox.Location = new System.Drawing.Point(15, 198);
            this.summaryBox.Multiline = true;
            this.summaryBox.Name = "summaryBox";
            this.summaryBox.ReadOnly = true;
            this.summaryBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.summaryBox.Size = new System.Drawing.Size(537, 170);
            this.summaryBox.TabIndex = 6;
            this.summaryBox.TabStop = false;
            //
            // previewBtn
            //
            this.previewBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.previewBtn.Location = new System.Drawing.Point(15, 380);
            this.previewBtn.Name = "previewBtn";
            this.previewBtn.Size = new System.Drawing.Size(130, 28);
            this.previewBtn.TabIndex = 7;
            this.previewBtn.Text = "Preview...";
            this.previewBtn.UseVisualStyleBackColor = true;
            //
            // importBtn
            //
            this.importBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.importBtn.Location = new System.Drawing.Point(422, 380);
            this.importBtn.Name = "importBtn";
            this.importBtn.Size = new System.Drawing.Size(130, 28);
            this.importBtn.TabIndex = 9;
            this.importBtn.Text = "Import";
            this.importBtn.UseVisualStyleBackColor = true;
            //
            // cancelBtn
            //
            this.cancelBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelBtn.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelBtn.Location = new System.Drawing.Point(312, 380);
            this.cancelBtn.Name = "cancelBtn";
            this.cancelBtn.Size = new System.Drawing.Size(100, 28);
            this.cancelBtn.TabIndex = 8;
            this.cancelBtn.Text = "Cancel";
            this.cancelBtn.UseVisualStyleBackColor = true;
            //
            // ImportAnimation
            //
            this.AcceptButton = this.importBtn;
            this.CancelButton = this.cancelBtn;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(567, 421);
            this.Controls.Add(this.fromLabel);
            this.Controls.Add(this.fileLabel);
            this.Controls.Add(this.nameLabel);
            this.Controls.Add(this.nameBox);
            this.Controls.Add(this.pathLabel);
            this.Controls.Add(this.pathBox);
            this.Controls.Add(this.rigLabel);
            this.Controls.Add(this.rigBox);
            this.Controls.Add(this.rootLabel);
            this.Controls.Add(this.rootBox);
            this.Controls.Add(this.rateLabel);
            this.Controls.Add(this.rateBox);
            this.Controls.Add(this.additiveCheck);
            this.Controls.Add(this.summaryBox);
            this.Controls.Add(this.previewBtn);
            this.Controls.Add(this.cancelBtn);
            this.Controls.Add(this.importBtn);
            this.MinimumSize = new System.Drawing.Size(500, 400);
            this.Name = "ImportAnimation";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Import animation";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label fromLabel;
        private System.Windows.Forms.Label fileLabel;
        private System.Windows.Forms.Label nameLabel;
        private System.Windows.Forms.TextBox nameBox;
        private System.Windows.Forms.Label pathLabel;
        private System.Windows.Forms.TextBox pathBox;
        private System.Windows.Forms.Label rigLabel;
        private System.Windows.Forms.ComboBox rigBox;
        private System.Windows.Forms.Label rootLabel;
        private System.Windows.Forms.ComboBox rootBox;
        private System.Windows.Forms.Label rateLabel;
        private System.Windows.Forms.ComboBox rateBox;
        private System.Windows.Forms.CheckBox additiveCheck;
        private System.Windows.Forms.TextBox summaryBox;
        private System.Windows.Forms.Button previewBtn;
        private System.Windows.Forms.Button importBtn;
        private System.Windows.Forms.Button cancelBtn;
    }
}

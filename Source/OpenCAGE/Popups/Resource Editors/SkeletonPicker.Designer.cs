namespace AlienPAK
{
    partial class SkeletonPicker
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.statusLabel = new System.Windows.Forms.Label();
            this.searchBox = new System.Windows.Forms.TextBox();
            this.searchPanel = new System.Windows.Forms.Panel();
            this.searchLabel = new System.Windows.Forms.Label();
            this.skeletonList = new System.Windows.Forms.ListView();
            this.panelButtons = new System.Windows.Forms.Panel();
            this.okBtn = new System.Windows.Forms.Button();
            this.noneBtn = new System.Windows.Forms.Button();
            this.cancelBtn = new System.Windows.Forms.Button();
            this.searchPanel.SuspendLayout();
            this.panelButtons.SuspendLayout();
            this.SuspendLayout();
            //
            // statusLabel
            //
            this.statusLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this.statusLabel.Location = new System.Drawing.Point(0, 0);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Padding = new System.Windows.Forms.Padding(8, 6, 8, 6);
            this.statusLabel.Size = new System.Drawing.Size(520, 52);
            this.statusLabel.TabIndex = 0;
            this.statusLabel.Text = "Scoring skeletons against this model...";
            //
            // searchPanel
            //
            this.searchPanel.Controls.Add(this.searchBox);
            this.searchPanel.Controls.Add(this.searchLabel);
            this.searchPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.searchPanel.Location = new System.Drawing.Point(0, 52);
            this.searchPanel.Name = "searchPanel";
            this.searchPanel.Padding = new System.Windows.Forms.Padding(8, 0, 8, 6);
            this.searchPanel.Size = new System.Drawing.Size(520, 30);
            this.searchPanel.TabIndex = 1;
            //
            // searchLabel
            //
            this.searchLabel.AutoSize = true;
            this.searchLabel.Location = new System.Drawing.Point(8, 6);
            this.searchLabel.Name = "searchLabel";
            this.searchLabel.Size = new System.Drawing.Size(41, 13);
            this.searchLabel.TabIndex = 0;
            this.searchLabel.Text = "Search:";
            //
            // searchBox
            //
            this.searchBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right))));
            this.searchBox.Location = new System.Drawing.Point(56, 3);
            this.searchBox.Name = "searchBox";
            this.searchBox.Size = new System.Drawing.Size(456, 20);
            this.searchBox.TabIndex = 1;
            //
            // skeletonList
            //
            this.skeletonList.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.skeletonList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.skeletonList.FullRowSelect = true;
            this.skeletonList.HideSelection = false;
            this.skeletonList.Location = new System.Drawing.Point(0, 82);
            this.skeletonList.MultiSelect = false;
            this.skeletonList.Name = "skeletonList";
            this.skeletonList.Size = new System.Drawing.Size(520, 316);
            this.skeletonList.TabIndex = 2;
            this.skeletonList.UseCompatibleStateImageBehavior = false;
            this.skeletonList.View = System.Windows.Forms.View.Details;
            //
            // panelButtons
            //
            this.panelButtons.Controls.Add(this.okBtn);
            this.panelButtons.Controls.Add(this.noneBtn);
            this.panelButtons.Controls.Add(this.cancelBtn);
            this.panelButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelButtons.Location = new System.Drawing.Point(0, 398);
            this.panelButtons.Name = "panelButtons";
            this.panelButtons.Size = new System.Drawing.Size(520, 42);
            this.panelButtons.TabIndex = 3;
            //
            // okBtn
            //
            this.okBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.okBtn.Location = new System.Drawing.Point(348, 8);
            this.okBtn.Name = "okBtn";
            this.okBtn.Size = new System.Drawing.Size(80, 25);
            this.okBtn.TabIndex = 0;
            this.okBtn.Text = "Export";
            this.okBtn.UseVisualStyleBackColor = true;
            //
            // noneBtn
            //
            this.noneBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)));
            this.noneBtn.Location = new System.Drawing.Point(8, 8);
            this.noneBtn.Name = "noneBtn";
            this.noneBtn.Size = new System.Drawing.Size(130, 25);
            this.noneBtn.TabIndex = 1;
            this.noneBtn.Text = "Export without one";
            this.noneBtn.UseVisualStyleBackColor = true;
            //
            // cancelBtn
            //
            this.cancelBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelBtn.Location = new System.Drawing.Point(434, 8);
            this.cancelBtn.Name = "cancelBtn";
            this.cancelBtn.Size = new System.Drawing.Size(80, 25);
            this.cancelBtn.TabIndex = 2;
            this.cancelBtn.Text = "Cancel";
            this.cancelBtn.UseVisualStyleBackColor = true;
            //
            // SkeletonPicker
            //
            this.AcceptButton = this.okBtn;
            this.CancelButton = this.cancelBtn;
            this.ClientSize = new System.Drawing.Size(520, 440);
            this.Controls.Add(this.skeletonList);
            this.Controls.Add(this.searchPanel);
            this.Controls.Add(this.statusLabel);
            this.Controls.Add(this.panelButtons);
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.Name = "SkeletonPicker";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Choose a skeleton";
            this.searchPanel.ResumeLayout(false);
            this.searchPanel.PerformLayout();
            this.panelButtons.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.Panel searchPanel;
        private System.Windows.Forms.Label searchLabel;
        private System.Windows.Forms.TextBox searchBox;
        private System.Windows.Forms.ListView skeletonList;
        private System.Windows.Forms.Panel panelButtons;
        private System.Windows.Forms.Button okBtn;
        private System.Windows.Forms.Button noneBtn;
        private System.Windows.Forms.Button cancelBtn;
    }
}

namespace OpenCAGE.Popups
{
    partial class ShowConnectedLinks
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
            this.goToButton = new System.Windows.Forms.Button();
            this.linkList = new System.Windows.Forms.ListView();
            this.pinColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.nodeColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.linkedPinColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.findLabel = new System.Windows.Forms.Label();
            this.findBox = new System.Windows.Forms.TextBox();
            this.countLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // goToButton
            //
            this.goToButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.goToButton.Location = new System.Drawing.Point(245, 544);
            this.goToButton.Name = "goToButton";
            this.goToButton.Size = new System.Drawing.Size(132, 31);
            this.goToButton.TabIndex = 4;
            this.goToButton.Text = "Go To";
            this.goToButton.UseVisualStyleBackColor = true;
            this.goToButton.Click += new System.EventHandler(this.goToButton_Click);
            //
            // linkList
            //
            this.linkList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.linkList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.pinColumn,
            this.nodeColumn,
            this.linkedPinColumn});
            this.linkList.FullRowSelect = true;
            this.linkList.HideSelection = false;
            this.linkList.Location = new System.Drawing.Point(12, 38);
            this.linkList.MultiSelect = false;
            this.linkList.Name = "linkList";
            this.linkList.ShowItemToolTips = true;
            this.linkList.Size = new System.Drawing.Size(365, 500);
            this.linkList.TabIndex = 2;
            this.linkList.UseCompatibleStateImageBehavior = false;
            this.linkList.View = System.Windows.Forms.View.Details;
            this.linkList.KeyDown += new System.Windows.Forms.KeyEventHandler(this.linkList_KeyDown);
            this.linkList.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.linkList_MouseDoubleClick);
            //
            // pinColumn
            //
            this.pinColumn.Text = "Pin";
            this.pinColumn.Width = 95;
            //
            // nodeColumn
            //
            this.nodeColumn.Text = "Linked Node";
            this.nodeColumn.Width = 150;
            //
            // linkedPinColumn
            //
            this.linkedPinColumn.Text = "Linked Pin";
            this.linkedPinColumn.Width = 95;
            //
            // findLabel
            //
            this.findLabel.AutoSize = true;
            this.findLabel.Location = new System.Drawing.Point(12, 15);
            this.findLabel.Name = "findLabel";
            this.findLabel.Size = new System.Drawing.Size(30, 13);
            this.findLabel.TabIndex = 0;
            this.findLabel.Text = "Find:";
            //
            // findBox
            //
            this.findBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.findBox.Location = new System.Drawing.Point(48, 12);
            this.findBox.Name = "findBox";
            this.findBox.Size = new System.Drawing.Size(329, 20);
            this.findBox.TabIndex = 1;
            this.findBox.TextChanged += new System.EventHandler(this.findBox_TextChanged);
            this.findBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.findBox_KeyDown);
            //
            // countLabel
            //
            this.countLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.countLabel.AutoEllipsis = true;
            this.countLabel.Location = new System.Drawing.Point(12, 544);
            this.countLabel.Name = "countLabel";
            this.countLabel.Size = new System.Drawing.Size(227, 31);
            this.countLabel.TabIndex = 3;
            this.countLabel.Text = "0 links.";
            this.countLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // ShowConnectedLinks
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(389, 587);
            this.Controls.Add(this.countLabel);
            this.Controls.Add(this.findBox);
            this.Controls.Add(this.findLabel);
            this.Controls.Add(this.linkList);
            this.Controls.Add(this.goToButton);
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.Name = "ShowConnectedLinks";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Connected Links";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button goToButton;
        private System.Windows.Forms.ListView linkList;
        private System.Windows.Forms.ColumnHeader pinColumn;
        private System.Windows.Forms.ColumnHeader nodeColumn;
        private System.Windows.Forms.ColumnHeader linkedPinColumn;
        private System.Windows.Forms.Label findLabel;
        private System.Windows.Forms.TextBox findBox;
        private System.Windows.Forms.Label countLabel;
    }
}

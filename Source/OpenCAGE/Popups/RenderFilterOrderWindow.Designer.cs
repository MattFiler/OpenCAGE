namespace OpenCAGE.Popups
{
    partial class RenderFilterOrderWindow
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
            this.introLabel = new System.Windows.Forms.Label();
            this.orderList = new System.Windows.Forms.ListBox();
            this.addDividerButton = new System.Windows.Forms.Button();
            this.removeDividerButton = new System.Windows.Forms.Button();
            this.moveUpButton = new System.Windows.Forms.Button();
            this.moveDownButton = new System.Windows.Forms.Button();
            this.resetButton = new System.Windows.Forms.Button();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // introLabel
            //
            this.introLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.introLabel.Location = new System.Drawing.Point(12, 9);
            this.introLabel.Name = "introLabel";
            this.introLabel.Size = new System.Drawing.Size(336, 30);
            this.introLabel.TabIndex = 0;
            this.introLabel.Text = "Drag the filters into the order you want. Dividers draw a line between groups in the Render Filters panel.";
            //
            // orderList
            //
            this.orderList.AllowDrop = true;
            this.orderList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.orderList.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.orderList.IntegralHeight = false;
            this.orderList.ItemHeight = 20;
            this.orderList.Location = new System.Drawing.Point(12, 44);
            this.orderList.Name = "orderList";
            this.orderList.Size = new System.Drawing.Size(240, 368);
            this.orderList.TabIndex = 1;
            this.orderList.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.orderList_DrawItem);
            this.orderList.SelectedIndexChanged += new System.EventHandler(this.orderList_SelectedIndexChanged);
            this.orderList.DragDrop += new System.Windows.Forms.DragEventHandler(this.orderList_DragDrop);
            this.orderList.DragOver += new System.Windows.Forms.DragEventHandler(this.orderList_DragOver);
            this.orderList.DragLeave += new System.EventHandler(this.orderList_DragLeave);
            this.orderList.KeyDown += new System.Windows.Forms.KeyEventHandler(this.orderList_KeyDown);
            this.orderList.MouseDown += new System.Windows.Forms.MouseEventHandler(this.orderList_MouseDown);
            this.orderList.MouseMove += new System.Windows.Forms.MouseEventHandler(this.orderList_MouseMove);
            //
            // addDividerButton
            //
            this.addDividerButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.addDividerButton.Location = new System.Drawing.Point(258, 44);
            this.addDividerButton.Name = "addDividerButton";
            this.addDividerButton.Size = new System.Drawing.Size(90, 23);
            this.addDividerButton.TabIndex = 2;
            this.addDividerButton.Text = "Add Divider";
            this.addDividerButton.UseVisualStyleBackColor = true;
            this.addDividerButton.Click += new System.EventHandler(this.addDividerButton_Click);
            //
            // removeDividerButton
            //
            this.removeDividerButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.removeDividerButton.Location = new System.Drawing.Point(258, 73);
            this.removeDividerButton.Name = "removeDividerButton";
            this.removeDividerButton.Size = new System.Drawing.Size(90, 23);
            this.removeDividerButton.TabIndex = 3;
            this.removeDividerButton.Text = "Remove Divider";
            this.removeDividerButton.UseVisualStyleBackColor = true;
            this.removeDividerButton.Click += new System.EventHandler(this.removeDividerButton_Click);
            //
            // moveUpButton
            //
            this.moveUpButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.moveUpButton.Location = new System.Drawing.Point(258, 110);
            this.moveUpButton.Name = "moveUpButton";
            this.moveUpButton.Size = new System.Drawing.Size(90, 23);
            this.moveUpButton.TabIndex = 4;
            this.moveUpButton.Text = "Move Up";
            this.moveUpButton.UseVisualStyleBackColor = true;
            this.moveUpButton.Click += new System.EventHandler(this.moveUpButton_Click);
            //
            // moveDownButton
            //
            this.moveDownButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.moveDownButton.Location = new System.Drawing.Point(258, 139);
            this.moveDownButton.Name = "moveDownButton";
            this.moveDownButton.Size = new System.Drawing.Size(90, 23);
            this.moveDownButton.TabIndex = 5;
            this.moveDownButton.Text = "Move Down";
            this.moveDownButton.UseVisualStyleBackColor = true;
            this.moveDownButton.Click += new System.EventHandler(this.moveDownButton_Click);
            //
            // resetButton
            //
            this.resetButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.resetButton.Location = new System.Drawing.Point(258, 176);
            this.resetButton.Name = "resetButton";
            this.resetButton.Size = new System.Drawing.Size(90, 23);
            this.resetButton.TabIndex = 6;
            this.resetButton.Text = "Reset Order";
            this.resetButton.UseVisualStyleBackColor = true;
            this.resetButton.Click += new System.EventHandler(this.resetButton_Click);
            //
            // okButton
            //
            this.okButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.okButton.Location = new System.Drawing.Point(192, 425);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 23);
            this.okButton.TabIndex = 7;
            this.okButton.Text = "OK";
            this.okButton.UseVisualStyleBackColor = true;
            //
            // cancelButton
            //
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(273, 425);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 8;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            //
            // RenderFilterOrderWindow
            //
            this.AcceptButton = this.okButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(360, 460);
            this.Controls.Add(this.introLabel);
            this.Controls.Add(this.orderList);
            this.Controls.Add(this.addDividerButton);
            this.Controls.Add(this.removeDividerButton);
            this.Controls.Add(this.moveUpButton);
            this.Controls.Add(this.moveDownButton);
            this.Controls.Add(this.resetButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.cancelButton);
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(376, 339);
            this.Name = "RenderFilterOrderWindow";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Render Filter Order";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.RenderFilterOrderWindow_FormClosed);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label introLabel;
        private System.Windows.Forms.ListBox orderList;
        private System.Windows.Forms.Button addDividerButton;
        private System.Windows.Forms.Button removeDividerButton;
        private System.Windows.Forms.Button moveUpButton;
        private System.Windows.Forms.Button moveDownButton;
        private System.Windows.Forms.Button resetButton;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
    }
}

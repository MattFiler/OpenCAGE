namespace OpenCAGE.Popups
{
    partial class SnapIncrements
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
            this.gridGroup = new System.Windows.Forms.GroupBox();
            this._gridList = new System.Windows.Forms.ListBox();
            this._gridInput = new System.Windows.Forms.NumericUpDown();
            this.gridAddButton = new System.Windows.Forms.Button();
            this.gridRemoveButton = new System.Windows.Forms.Button();
            this.rotationGroup = new System.Windows.Forms.GroupBox();
            this._rotationList = new System.Windows.Forms.ListBox();
            this._rotationInput = new System.Windows.Forms.NumericUpDown();
            this.rotationAddButton = new System.Windows.Forms.Button();
            this.rotationRemoveButton = new System.Windows.Forms.Button();
            this.resetButton = new System.Windows.Forms.Button();
            this.closeButton = new System.Windows.Forms.Button();
            this.gridGroup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._gridInput)).BeginInit();
            this.rotationGroup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._rotationInput)).BeginInit();
            this.SuspendLayout();
            //
            // gridGroup
            //
            this.gridGroup.Controls.Add(this._gridList);
            this.gridGroup.Controls.Add(this._gridInput);
            this.gridGroup.Controls.Add(this.gridAddButton);
            this.gridGroup.Controls.Add(this.gridRemoveButton);
            this.gridGroup.Location = new System.Drawing.Point(10, 10);
            this.gridGroup.Name = "gridGroup";
            this.gridGroup.Size = new System.Drawing.Size(200, 265);
            this.gridGroup.TabIndex = 0;
            this.gridGroup.TabStop = false;
            this.gridGroup.Text = "Transform (units)";
            //
            // _gridList
            //
            this._gridList.IntegralHeight = false;
            this._gridList.Location = new System.Drawing.Point(12, 24);
            this._gridList.Name = "_gridList";
            this._gridList.Size = new System.Drawing.Size(176, 165);
            this._gridList.TabIndex = 0;
            //
            // _gridInput
            //
            this._gridInput.DecimalPlaces = 3;
            this._gridInput.Increment = new decimal(new int[] {
            5,
            0,
            0,
            131072});
            this._gridInput.Location = new System.Drawing.Point(12, 200);
            this._gridInput.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this._gridInput.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            196608});
            this._gridInput.Name = "_gridInput";
            this._gridInput.Size = new System.Drawing.Size(90, 20);
            this._gridInput.TabIndex = 1;
            this._gridInput.Value = new decimal(new int[] {
            25,
            0,
            0,
            131072});
            //
            // gridAddButton
            //
            this.gridAddButton.Location = new System.Drawing.Point(108, 200);
            this.gridAddButton.Name = "gridAddButton";
            this.gridAddButton.Size = new System.Drawing.Size(80, 24);
            this.gridAddButton.TabIndex = 2;
            this.gridAddButton.Text = "Add";
            this.gridAddButton.Click += new System.EventHandler(this.AddGrid);
            //
            // gridRemoveButton
            //
            this.gridRemoveButton.Location = new System.Drawing.Point(12, 230);
            this.gridRemoveButton.Name = "gridRemoveButton";
            this.gridRemoveButton.Size = new System.Drawing.Size(176, 26);
            this.gridRemoveButton.TabIndex = 3;
            this.gridRemoveButton.Text = "Remove Selected";
            this.gridRemoveButton.Click += new System.EventHandler(this.RemoveGrid);
            //
            // rotationGroup
            //
            this.rotationGroup.Controls.Add(this._rotationList);
            this.rotationGroup.Controls.Add(this._rotationInput);
            this.rotationGroup.Controls.Add(this.rotationAddButton);
            this.rotationGroup.Controls.Add(this.rotationRemoveButton);
            this.rotationGroup.Location = new System.Drawing.Point(220, 10);
            this.rotationGroup.Name = "rotationGroup";
            this.rotationGroup.Size = new System.Drawing.Size(200, 265);
            this.rotationGroup.TabIndex = 1;
            this.rotationGroup.TabStop = false;
            this.rotationGroup.Text = "Rotation (degrees)";
            //
            // _rotationList
            //
            this._rotationList.IntegralHeight = false;
            this._rotationList.Location = new System.Drawing.Point(12, 24);
            this._rotationList.Name = "_rotationList";
            this._rotationList.Size = new System.Drawing.Size(176, 165);
            this._rotationList.TabIndex = 0;
            //
            // _rotationInput
            //
            this._rotationInput.DecimalPlaces = 2;
            this._rotationInput.Location = new System.Drawing.Point(12, 200);
            this._rotationInput.Maximum = new decimal(new int[] {
            360,
            0,
            0,
            0});
            this._rotationInput.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            131072});
            this._rotationInput.Name = "_rotationInput";
            this._rotationInput.Size = new System.Drawing.Size(90, 20);
            this._rotationInput.TabIndex = 1;
            this._rotationInput.Value = new decimal(new int[] {
            5,
            0,
            0,
            0});
            //
            // rotationAddButton
            //
            this.rotationAddButton.Location = new System.Drawing.Point(108, 200);
            this.rotationAddButton.Name = "rotationAddButton";
            this.rotationAddButton.Size = new System.Drawing.Size(80, 24);
            this.rotationAddButton.TabIndex = 2;
            this.rotationAddButton.Text = "Add";
            this.rotationAddButton.Click += new System.EventHandler(this.AddRotation);
            //
            // rotationRemoveButton
            //
            this.rotationRemoveButton.Location = new System.Drawing.Point(12, 230);
            this.rotationRemoveButton.Name = "rotationRemoveButton";
            this.rotationRemoveButton.Size = new System.Drawing.Size(176, 26);
            this.rotationRemoveButton.TabIndex = 3;
            this.rotationRemoveButton.Text = "Remove Selected";
            this.rotationRemoveButton.Click += new System.EventHandler(this.RemoveRotation);
            //
            // resetButton
            //
            this.resetButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.resetButton.Location = new System.Drawing.Point(10, 285);
            this.resetButton.Name = "resetButton";
            this.resetButton.Size = new System.Drawing.Size(130, 26);
            this.resetButton.TabIndex = 2;
            this.resetButton.Text = "Reset to Defaults";
            this.resetButton.Click += new System.EventHandler(this.resetButton_Click);
            //
            // closeButton
            //
            this.closeButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.closeButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.closeButton.Location = new System.Drawing.Point(340, 285);
            this.closeButton.Name = "closeButton";
            this.closeButton.Size = new System.Drawing.Size(80, 26);
            this.closeButton.TabIndex = 3;
            this.closeButton.Text = "Close";
            this.closeButton.Click += new System.EventHandler(this.closeButton_Click);
            //
            // SnapIncrements
            //
            this.AcceptButton = this.closeButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(430, 320);
            this.Controls.Add(this.gridGroup);
            this.Controls.Add(this.rotationGroup);
            this.Controls.Add(this.resetButton);
            this.Controls.Add(this.closeButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SnapIncrements";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Snap Increments";
            this.gridGroup.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._gridInput)).EndInit();
            this.rotationGroup.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._rotationInput)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox gridGroup;
        private System.Windows.Forms.ListBox _gridList;
        private System.Windows.Forms.NumericUpDown _gridInput;
        private System.Windows.Forms.Button gridAddButton;
        private System.Windows.Forms.Button gridRemoveButton;
        private System.Windows.Forms.GroupBox rotationGroup;
        private System.Windows.Forms.ListBox _rotationList;
        private System.Windows.Forms.NumericUpDown _rotationInput;
        private System.Windows.Forms.Button rotationAddButton;
        private System.Windows.Forms.Button rotationRemoveButton;
        private System.Windows.Forms.Button resetButton;
        private System.Windows.Forms.Button closeButton;
    }
}

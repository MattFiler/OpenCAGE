namespace OpenCAGE
{
    partial class PhysicsImportSettings
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
            this._grid = new System.Windows.Forms.TableLayoutPanel();
            this._systemNameLabel = new System.Windows.Forms.Label();
            this._systemName = new System.Windows.Forms.TextBox();
            this._bodyNameLabel = new System.Windows.Forms.Label();
            this._bodyName = new System.Windows.Forms.TextBox();
            this._massLabel = new System.Windows.Forms.Label();
            this._mass = new System.Windows.Forms.NumericUpDown();
            this._frictionLabel = new System.Windows.Forms.Label();
            this._friction = new System.Windows.Forms.NumericUpDown();
            this._restitutionLabel = new System.Windows.Forms.Label();
            this._restitution = new System.Windows.Forms.NumericUpDown();
            this._placementLabel = new System.Windows.Forms.Label();
            this._placeAtModel = new System.Windows.Forms.CheckBox();
            this._summary = new System.Windows.Forms.Label();
            this._buttons = new System.Windows.Forms.FlowLayoutPanel();
            this._cancel = new System.Windows.Forms.Button();
            this._create = new System.Windows.Forms.Button();
            this._grid.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._mass)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._friction)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._restitution)).BeginInit();
            this._buttons.SuspendLayout();
            this.SuspendLayout();
            //
            // _grid
            //
            this._grid.ColumnCount = 2;
            this._grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this._grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._grid.Controls.Add(this._systemNameLabel, 0, 0);
            this._grid.Controls.Add(this._systemName, 1, 0);
            this._grid.Controls.Add(this._bodyNameLabel, 0, 1);
            this._grid.Controls.Add(this._bodyName, 1, 1);
            this._grid.Controls.Add(this._massLabel, 0, 2);
            this._grid.Controls.Add(this._mass, 1, 2);
            this._grid.Controls.Add(this._frictionLabel, 0, 3);
            this._grid.Controls.Add(this._friction, 1, 3);
            this._grid.Controls.Add(this._restitutionLabel, 0, 4);
            this._grid.Controls.Add(this._restitution, 1, 4);
            this._grid.Controls.Add(this._placementLabel, 0, 5);
            this._grid.Controls.Add(this._placeAtModel, 1, 5);
            this._grid.Dock = System.Windows.Forms.DockStyle.Fill;
            this._grid.Location = new System.Drawing.Point(12, 60);
            this._grid.Name = "_grid";
            this._grid.RowCount = 6;
            this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this._grid.Size = new System.Drawing.Size(416, 182);
            this._grid.TabIndex = 0;
            //
            // _systemNameLabel
            //
            this._systemNameLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._systemNameLabel.Location = new System.Drawing.Point(3, 0);
            this._systemNameLabel.Name = "_systemNameLabel";
            this._systemNameLabel.Size = new System.Drawing.Size(114, 30);
            this._systemNameLabel.TabIndex = 0;
            this._systemNameLabel.Text = "System name";
            this._systemNameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _systemName
            //
            this._systemName.Dock = System.Windows.Forms.DockStyle.Fill;
            this._systemName.Location = new System.Drawing.Point(123, 3);
            this._systemName.Name = "_systemName";
            this._systemName.Size = new System.Drawing.Size(290, 20);
            this._systemName.TabIndex = 1;
            //
            // _bodyNameLabel
            //
            this._bodyNameLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._bodyNameLabel.Location = new System.Drawing.Point(3, 30);
            this._bodyNameLabel.Name = "_bodyNameLabel";
            this._bodyNameLabel.Size = new System.Drawing.Size(114, 30);
            this._bodyNameLabel.TabIndex = 2;
            this._bodyNameLabel.Text = "Body name";
            this._bodyNameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _bodyName
            //
            this._bodyName.Dock = System.Windows.Forms.DockStyle.Fill;
            this._bodyName.Location = new System.Drawing.Point(123, 33);
            this._bodyName.Name = "_bodyName";
            this._bodyName.Size = new System.Drawing.Size(290, 20);
            this._bodyName.TabIndex = 3;
            //
            // _massLabel
            //
            this._massLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._massLabel.Location = new System.Drawing.Point(3, 60);
            this._massLabel.Name = "_massLabel";
            this._massLabel.Size = new System.Drawing.Size(114, 30);
            this._massLabel.TabIndex = 4;
            this._massLabel.Text = "Mass (kg)";
            this._massLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _mass
            //
            this._mass.DecimalPlaces = 2;
            this._mass.Dock = System.Windows.Forms.DockStyle.Left;
            this._mass.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this._mass.Location = new System.Drawing.Point(123, 63);
            this._mass.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this._mass.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            131072});
            this._mass.Name = "_mass";
            this._mass.Size = new System.Drawing.Size(110, 20);
            this._mass.TabIndex = 5;
            this._mass.Value = new decimal(new int[] {
            1,
            0,
            0,
            131072});
            //
            // _frictionLabel
            //
            this._frictionLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._frictionLabel.Location = new System.Drawing.Point(3, 90);
            this._frictionLabel.Name = "_frictionLabel";
            this._frictionLabel.Size = new System.Drawing.Size(114, 30);
            this._frictionLabel.TabIndex = 6;
            this._frictionLabel.Text = "Friction";
            this._frictionLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _friction
            //
            this._friction.DecimalPlaces = 2;
            this._friction.Dock = System.Windows.Forms.DockStyle.Left;
            this._friction.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this._friction.Location = new System.Drawing.Point(123, 93);
            this._friction.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this._friction.Name = "_friction";
            this._friction.Size = new System.Drawing.Size(110, 20);
            this._friction.TabIndex = 7;
            this._friction.Value = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            //
            // _restitutionLabel
            //
            this._restitutionLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._restitutionLabel.Location = new System.Drawing.Point(3, 120);
            this._restitutionLabel.Name = "_restitutionLabel";
            this._restitutionLabel.Size = new System.Drawing.Size(114, 30);
            this._restitutionLabel.TabIndex = 8;
            this._restitutionLabel.Text = "Restitution";
            this._restitutionLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _restitution
            //
            this._restitution.DecimalPlaces = 2;
            this._restitution.Dock = System.Windows.Forms.DockStyle.Left;
            this._restitution.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this._restitution.Location = new System.Drawing.Point(123, 123);
            this._restitution.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this._restitution.Name = "_restitution";
            this._restitution.Size = new System.Drawing.Size(110, 20);
            this._restitution.TabIndex = 9;
            this._restitution.Value = new decimal(new int[] {
            4,
            0,
            0,
            65536});
            //
            // _placementLabel
            //
            this._placementLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._placementLabel.Location = new System.Drawing.Point(3, 150);
            this._placementLabel.Name = "_placementLabel";
            this._placementLabel.Size = new System.Drawing.Size(114, 30);
            this._placementLabel.TabIndex = 10;
            this._placementLabel.Text = "Placement";
            this._placementLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _placeAtModel
            //
            this._placeAtModel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._placeAtModel.Location = new System.Drawing.Point(123, 153);
            this._placeAtModel.Name = "_placeAtModel";
            this._placeAtModel.Size = new System.Drawing.Size(290, 24);
            this._placeAtModel.TabIndex = 11;
            //
            // _summary
            //
            this._summary.Dock = System.Windows.Forms.DockStyle.Top;
            this._summary.Location = new System.Drawing.Point(12, 12);
            this._summary.Name = "_summary";
            this._summary.Size = new System.Drawing.Size(416, 48);
            this._summary.TabIndex = 1;
            //
            // _buttons
            //
            this._buttons.Controls.Add(this._cancel);
            this._buttons.Controls.Add(this._create);
            this._buttons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this._buttons.Location = new System.Drawing.Point(12, 242);
            this._buttons.Name = "_buttons";
            this._buttons.Size = new System.Drawing.Size(416, 36);
            this._buttons.TabIndex = 2;
            //
            // _cancel
            //
            this._cancel.AutoSize = true;
            this._cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._cancel.Location = new System.Drawing.Point(338, 3);
            this._cancel.Name = "_cancel";
            this._cancel.Size = new System.Drawing.Size(75, 23);
            this._cancel.TabIndex = 0;
            this._cancel.Text = "Cancel";
            //
            // _create
            //
            this._create.AutoSize = true;
            this._create.DialogResult = System.Windows.Forms.DialogResult.OK;
            this._create.Location = new System.Drawing.Point(257, 3);
            this._create.Name = "_create";
            this._create.Size = new System.Drawing.Size(75, 23);
            this._create.TabIndex = 1;
            this._create.Text = "Create System";
            //
            // PhysicsImportSettings
            //
            this.AcceptButton = this._create;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._cancel;
            this.ClientSize = new System.Drawing.Size(440, 290);
            this.Controls.Add(this._grid);
            this.Controls.Add(this._summary);
            this.Controls.Add(this._buttons);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PhysicsImportSettings";
            this.Padding = new System.Windows.Forms.Padding(12);
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "New Physics System";
            this._grid.ResumeLayout(false);
            this._grid.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._mass)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._friction)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._restitution)).EndInit();
            this._buttons.ResumeLayout(false);
            this._buttons.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel _grid;
        private System.Windows.Forms.Label _systemNameLabel;
        private System.Windows.Forms.TextBox _systemName;
        private System.Windows.Forms.Label _bodyNameLabel;
        private System.Windows.Forms.TextBox _bodyName;
        private System.Windows.Forms.Label _massLabel;
        private System.Windows.Forms.NumericUpDown _mass;
        private System.Windows.Forms.Label _frictionLabel;
        private System.Windows.Forms.NumericUpDown _friction;
        private System.Windows.Forms.Label _restitutionLabel;
        private System.Windows.Forms.NumericUpDown _restitution;
        private System.Windows.Forms.Label _placementLabel;
        private System.Windows.Forms.CheckBox _placeAtModel;
        private System.Windows.Forms.Label _summary;
        private System.Windows.Forms.FlowLayoutPanel _buttons;
        private System.Windows.Forms.Button _cancel;
        private System.Windows.Forms.Button _create;
    }
}

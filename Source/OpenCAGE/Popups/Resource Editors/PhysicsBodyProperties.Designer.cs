namespace OpenCAGE
{
    partial class PhysicsBodyProperties
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
            this._massLabel = new System.Windows.Forms.Label();
            this._mass = new System.Windows.Forms.NumericUpDown();
            this._frictionLabel = new System.Windows.Forms.Label();
            this._friction = new System.Windows.Forms.NumericUpDown();
            this._restitutionLabel = new System.Windows.Forms.Label();
            this._restitution = new System.Windows.Forms.NumericUpDown();
            this._linearDampingLabel = new System.Windows.Forms.Label();
            this._linearDamping = new System.Windows.Forms.NumericUpDown();
            this._angularDampingLabel = new System.Windows.Forms.Label();
            this._angularDamping = new System.Windows.Forms.NumericUpDown();
            this._gravityFactorLabel = new System.Windows.Forms.Label();
            this._gravityFactor = new System.Windows.Forms.NumericUpDown();
            this._summary = new System.Windows.Forms.Label();
            this._buttons = new System.Windows.Forms.FlowLayoutPanel();
            this._cancel = new System.Windows.Forms.Button();
            this._apply = new System.Windows.Forms.Button();
            this._grid.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._mass)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._friction)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._restitution)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._linearDamping)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._angularDamping)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._gravityFactor)).BeginInit();
            this._buttons.SuspendLayout();
            this.SuspendLayout();
            //
            // _grid
            //
            this._grid.ColumnCount = 2;
            this._grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 140F));
            this._grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._grid.Controls.Add(this._massLabel, 0, 0);
            this._grid.Controls.Add(this._mass, 1, 0);
            this._grid.Controls.Add(this._frictionLabel, 0, 1);
            this._grid.Controls.Add(this._friction, 1, 1);
            this._grid.Controls.Add(this._restitutionLabel, 0, 2);
            this._grid.Controls.Add(this._restitution, 1, 2);
            this._grid.Controls.Add(this._linearDampingLabel, 0, 3);
            this._grid.Controls.Add(this._linearDamping, 1, 3);
            this._grid.Controls.Add(this._angularDampingLabel, 0, 4);
            this._grid.Controls.Add(this._angularDamping, 1, 4);
            this._grid.Controls.Add(this._gravityFactorLabel, 0, 5);
            this._grid.Controls.Add(this._gravityFactor, 1, 5);
            this._grid.Dock = System.Windows.Forms.DockStyle.Fill;
            this._grid.Location = new System.Drawing.Point(12, 60);
            this._grid.Name = "_grid";
            this._grid.RowCount = 7;
            this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this._grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._grid.Size = new System.Drawing.Size(416, 192);
            this._grid.TabIndex = 0;
            //
            // _massLabel
            //
            this._massLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._massLabel.Location = new System.Drawing.Point(3, 0);
            this._massLabel.Name = "_massLabel";
            this._massLabel.Size = new System.Drawing.Size(134, 30);
            this._massLabel.TabIndex = 0;
            this._massLabel.Text = "Mass (kg)";
            this._massLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _mass
            //
            this._mass.DecimalPlaces = 3;
            this._mass.Dock = System.Windows.Forms.DockStyle.Left;
            this._mass.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this._mass.Location = new System.Drawing.Point(143, 3);
            this._mass.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this._mass.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            196608});
            this._mass.Name = "_mass";
            this._mass.Size = new System.Drawing.Size(110, 20);
            this._mass.TabIndex = 1;
            this._mass.Value = new decimal(new int[] {
            1,
            0,
            0,
            196608});
            //
            // _frictionLabel
            //
            this._frictionLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._frictionLabel.Location = new System.Drawing.Point(3, 30);
            this._frictionLabel.Name = "_frictionLabel";
            this._frictionLabel.Size = new System.Drawing.Size(134, 30);
            this._frictionLabel.TabIndex = 2;
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
            this._friction.Location = new System.Drawing.Point(143, 33);
            this._friction.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this._friction.Name = "_friction";
            this._friction.Size = new System.Drawing.Size(110, 20);
            this._friction.TabIndex = 3;
            //
            // _restitutionLabel
            //
            this._restitutionLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._restitutionLabel.Location = new System.Drawing.Point(3, 60);
            this._restitutionLabel.Name = "_restitutionLabel";
            this._restitutionLabel.Size = new System.Drawing.Size(134, 30);
            this._restitutionLabel.TabIndex = 4;
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
            this._restitution.Location = new System.Drawing.Point(143, 63);
            this._restitution.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this._restitution.Name = "_restitution";
            this._restitution.Size = new System.Drawing.Size(110, 20);
            this._restitution.TabIndex = 5;
            //
            // _linearDampingLabel
            //
            this._linearDampingLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._linearDampingLabel.Location = new System.Drawing.Point(3, 90);
            this._linearDampingLabel.Name = "_linearDampingLabel";
            this._linearDampingLabel.Size = new System.Drawing.Size(134, 30);
            this._linearDampingLabel.TabIndex = 6;
            this._linearDampingLabel.Text = "Linear damping";
            this._linearDampingLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _linearDamping
            //
            this._linearDamping.DecimalPlaces = 3;
            this._linearDamping.Dock = System.Windows.Forms.DockStyle.Left;
            this._linearDamping.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this._linearDamping.Location = new System.Drawing.Point(143, 93);
            this._linearDamping.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this._linearDamping.Name = "_linearDamping";
            this._linearDamping.Size = new System.Drawing.Size(110, 20);
            this._linearDamping.TabIndex = 7;
            //
            // _angularDampingLabel
            //
            this._angularDampingLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._angularDampingLabel.Location = new System.Drawing.Point(3, 120);
            this._angularDampingLabel.Name = "_angularDampingLabel";
            this._angularDampingLabel.Size = new System.Drawing.Size(134, 30);
            this._angularDampingLabel.TabIndex = 8;
            this._angularDampingLabel.Text = "Angular damping";
            this._angularDampingLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _angularDamping
            //
            this._angularDamping.DecimalPlaces = 3;
            this._angularDamping.Dock = System.Windows.Forms.DockStyle.Left;
            this._angularDamping.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this._angularDamping.Location = new System.Drawing.Point(143, 123);
            this._angularDamping.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this._angularDamping.Name = "_angularDamping";
            this._angularDamping.Size = new System.Drawing.Size(110, 20);
            this._angularDamping.TabIndex = 9;
            //
            // _gravityFactorLabel
            //
            this._gravityFactorLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._gravityFactorLabel.Location = new System.Drawing.Point(3, 150);
            this._gravityFactorLabel.Name = "_gravityFactorLabel";
            this._gravityFactorLabel.Size = new System.Drawing.Size(134, 30);
            this._gravityFactorLabel.TabIndex = 10;
            this._gravityFactorLabel.Text = "Gravity factor";
            this._gravityFactorLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _gravityFactor
            //
            this._gravityFactor.DecimalPlaces = 2;
            this._gravityFactor.Dock = System.Windows.Forms.DockStyle.Left;
            this._gravityFactor.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this._gravityFactor.Location = new System.Drawing.Point(143, 153);
            this._gravityFactor.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this._gravityFactor.Name = "_gravityFactor";
            this._gravityFactor.Size = new System.Drawing.Size(110, 20);
            this._gravityFactor.TabIndex = 11;
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
            this._buttons.Controls.Add(this._apply);
            this._buttons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this._buttons.Location = new System.Drawing.Point(12, 252);
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
            // _apply
            //
            this._apply.AutoSize = true;
            this._apply.DialogResult = System.Windows.Forms.DialogResult.OK;
            this._apply.Location = new System.Drawing.Point(257, 3);
            this._apply.Name = "_apply";
            this._apply.Size = new System.Drawing.Size(75, 23);
            this._apply.TabIndex = 1;
            this._apply.Text = "Apply";
            //
            // PhysicsBodyProperties
            //
            this.AcceptButton = this._apply;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._cancel;
            this.ClientSize = new System.Drawing.Size(440, 300);
            this.Controls.Add(this._grid);
            this.Controls.Add(this._summary);
            this.Controls.Add(this._buttons);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PhysicsBodyProperties";
            this.Padding = new System.Windows.Forms.Padding(12);
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Rigid Body";
            this._grid.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._mass)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._friction)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._restitution)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._linearDamping)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._angularDamping)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._gravityFactor)).EndInit();
            this._buttons.ResumeLayout(false);
            this._buttons.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel _grid;
        private System.Windows.Forms.Label _massLabel;
        private System.Windows.Forms.NumericUpDown _mass;
        private System.Windows.Forms.Label _frictionLabel;
        private System.Windows.Forms.NumericUpDown _friction;
        private System.Windows.Forms.Label _restitutionLabel;
        private System.Windows.Forms.NumericUpDown _restitution;
        private System.Windows.Forms.Label _linearDampingLabel;
        private System.Windows.Forms.NumericUpDown _linearDamping;
        private System.Windows.Forms.Label _angularDampingLabel;
        private System.Windows.Forms.NumericUpDown _angularDamping;
        private System.Windows.Forms.Label _gravityFactorLabel;
        private System.Windows.Forms.NumericUpDown _gravityFactor;
        private System.Windows.Forms.Label _summary;
        private System.Windows.Forms.FlowLayoutPanel _buttons;
        private System.Windows.Forms.Button _cancel;
        private System.Windows.Forms.Button _apply;
    }
}

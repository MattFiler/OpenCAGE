namespace OpenCAGE
{
    partial class SpinDialog
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
            this.rootPanel = new System.Windows.Forms.FlowLayoutPanel();
            this._title = new System.Windows.Forms.Label();
            this.introLabel = new System.Windows.Forms.Label();
            this.axisRow = new System.Windows.Forms.FlowLayoutPanel();
            this.axisLabel = new System.Windows.Forms.Label();
            this._x = new System.Windows.Forms.RadioButton();
            this._y = new System.Windows.Forms.RadioButton();
            this._z = new System.Windows.Forms.RadioButton();
            this.speedLabel = new System.Windows.Forms.Label();
            this._speed = new System.Windows.Forms.NumericUpDown();
            this._period = new System.Windows.Forms.Label();
            this._suggest = new System.Windows.Forms.Label();
            this.previewHost = new System.Windows.Forms.Panel();
            this._status = new System.Windows.Forms.Label();
            this.buttonsRow = new System.Windows.Forms.FlowLayoutPanel();
            this._apply = new System.Windows.Forms.Button();
            this._remove = new System.Windows.Forms.Button();
            this._close = new System.Windows.Forms.Button();
            this.rootPanel.SuspendLayout();
            this.axisRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._speed)).BeginInit();
            this.buttonsRow.SuspendLayout();
            this.SuspendLayout();
            //
            // rootPanel
            //
            this.rootPanel.AutoSize = true;
            this.rootPanel.Controls.Add(this._title);
            this.rootPanel.Controls.Add(this.introLabel);
            this.rootPanel.Controls.Add(this.axisRow);
            this.rootPanel.Controls.Add(this._suggest);
            this.rootPanel.Controls.Add(this.previewHost);
            this.rootPanel.Controls.Add(this._status);
            this.rootPanel.Controls.Add(this.buttonsRow);
            this.rootPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.rootPanel.Location = new System.Drawing.Point(0, 0);
            this.rootPanel.Name = "rootPanel";
            this.rootPanel.Padding = new System.Windows.Forms.Padding(10);
            this.rootPanel.Size = new System.Drawing.Size(446, 378);
            this.rootPanel.TabIndex = 0;
            this.rootPanel.WrapContents = false;
            //
            // _title
            //
            this._title.AutoSize = true;
            this._title.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
            this._title.Location = new System.Drawing.Point(13, 10);
            this._title.Name = "_title";
            this._title.Size = new System.Drawing.Size(0, 13);
            this._title.TabIndex = 0;
            //
            // introLabel
            //
            this.introLabel.AutoSize = true;
            this.introLabel.Location = new System.Drawing.Point(13, 23);
            this.introLabel.MaximumSize = new System.Drawing.Size(420, 0);
            this.introLabel.Name = "introLabel";
            this.introLabel.Size = new System.Drawing.Size(413, 26);
            this.introLabel.TabIndex = 1;
            this.introLabel.Text = "It turns about one of its OWN axes for as long as the level runs (its rotation in the level sets where those point). Negative speeds turn it the other way.";
            //
            // axisRow
            //
            this.axisRow.AutoSize = true;
            this.axisRow.Controls.Add(this.axisLabel);
            this.axisRow.Controls.Add(this._x);
            this.axisRow.Controls.Add(this._y);
            this.axisRow.Controls.Add(this._z);
            this.axisRow.Controls.Add(this.speedLabel);
            this.axisRow.Controls.Add(this._speed);
            this.axisRow.Controls.Add(this._period);
            this.axisRow.Location = new System.Drawing.Point(13, 52);
            this.axisRow.Name = "axisRow";
            this.axisRow.Size = new System.Drawing.Size(369, 26);
            this.axisRow.TabIndex = 2;
            this.axisRow.WrapContents = false;
            //
            // axisLabel
            //
            this.axisLabel.AutoSize = true;
            this.axisLabel.Location = new System.Drawing.Point(3, 0);
            this.axisLabel.Name = "axisLabel";
            this.axisLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.axisLabel.Size = new System.Drawing.Size(29, 17);
            this.axisLabel.TabIndex = 0;
            this.axisLabel.Text = "Axis:";
            //
            // _x
            //
            this._x.AutoSize = true;
            this._x.Location = new System.Drawing.Point(38, 3);
            this._x.Name = "_x";
            this._x.Size = new System.Drawing.Size(32, 17);
            this._x.TabIndex = 1;
            this._x.Text = "X";
            this._x.CheckedChanged += new System.EventHandler(this.Turn_Changed);
            //
            // _y
            //
            this._y.AutoSize = true;
            this._y.Location = new System.Drawing.Point(76, 3);
            this._y.Name = "_y";
            this._y.Size = new System.Drawing.Size(32, 17);
            this._y.TabIndex = 2;
            this._y.Text = "Y";
            this._y.CheckedChanged += new System.EventHandler(this.Turn_Changed);
            //
            // _z
            //
            this._z.AutoSize = true;
            this._z.Location = new System.Drawing.Point(114, 3);
            this._z.Name = "_z";
            this._z.Size = new System.Drawing.Size(32, 17);
            this._z.TabIndex = 3;
            this._z.Text = "Z";
            this._z.CheckedChanged += new System.EventHandler(this.Turn_Changed);
            //
            // speedLabel
            //
            this.speedLabel.AutoSize = true;
            this.speedLabel.Location = new System.Drawing.Point(152, 0);
            this.speedLabel.Name = "speedLabel";
            this.speedLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.speedLabel.Size = new System.Drawing.Size(112, 17);
            this.speedLabel.TabIndex = 4;
            this.speedLabel.Text = "  Degrees per second:";
            //
            // _speed
            //
            this._speed.DecimalPlaces = 1;
            this._speed.Increment = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this._speed.Location = new System.Drawing.Point(270, 3);
            this._speed.Maximum = new decimal(new int[] {
            3600,
            0,
            0,
            0});
            this._speed.Minimum = new decimal(new int[] {
            3600,
            0,
            0,
            -2147483648});
            this._speed.Name = "_speed";
            this._speed.Size = new System.Drawing.Size(90, 20);
            this._speed.TabIndex = 5;
            this._speed.Value = new decimal(new int[] {
            90,
            0,
            0,
            0});
            this._speed.ValueChanged += new System.EventHandler(this.Turn_Changed);
            //
            // _period
            //
            this._period.AutoSize = true;
            this._period.Location = new System.Drawing.Point(366, 0);
            this._period.Name = "_period";
            this._period.Size = new System.Drawing.Size(0, 13);
            this._period.TabIndex = 6;
            //
            // _suggest
            //
            this._suggest.AutoSize = true;
            this._suggest.Location = new System.Drawing.Point(13, 81);
            this._suggest.MaximumSize = new System.Drawing.Size(420, 0);
            this._suggest.Name = "_suggest";
            this._suggest.Size = new System.Drawing.Size(0, 13);
            this._suggest.TabIndex = 3;
            //
            // previewHost
            //
            this.previewHost.Location = new System.Drawing.Point(13, 97);
            this.previewHost.Name = "previewHost";
            this.previewHost.Size = new System.Drawing.Size(420, 220);
            this.previewHost.TabIndex = 4;
            //
            // _status
            //
            this._status.AutoSize = true;
            this._status.ForeColor = System.Drawing.Color.Firebrick;
            this._status.Location = new System.Drawing.Point(13, 320);
            this._status.MaximumSize = new System.Drawing.Size(420, 0);
            this._status.Name = "_status";
            this._status.Size = new System.Drawing.Size(0, 13);
            this._status.TabIndex = 5;
            //
            // buttonsRow
            //
            this.buttonsRow.AutoSize = true;
            this.buttonsRow.Controls.Add(this._apply);
            this.buttonsRow.Controls.Add(this._remove);
            this.buttonsRow.Controls.Add(this._close);
            this.buttonsRow.Location = new System.Drawing.Point(13, 336);
            this.buttonsRow.Name = "buttonsRow";
            this.buttonsRow.Size = new System.Drawing.Size(246, 29);
            this.buttonsRow.TabIndex = 6;
            this.buttonsRow.WrapContents = false;
            //
            // _apply
            //
            this._apply.AutoSize = true;
            this._apply.Location = new System.Drawing.Point(3, 3);
            this._apply.Name = "_apply";
            this._apply.Size = new System.Drawing.Size(75, 23);
            this._apply.TabIndex = 0;
            this._apply.Text = "Apply";
            this._apply.Click += new System.EventHandler(this._apply_Click);
            //
            // _remove
            //
            this._remove.AutoSize = true;
            this._remove.Location = new System.Drawing.Point(84, 3);
            this._remove.Name = "_remove";
            this._remove.Size = new System.Drawing.Size(78, 23);
            this._remove.TabIndex = 1;
            this._remove.Text = "Stop Turning";
            this._remove.Click += new System.EventHandler(this._remove_Click);
            //
            // _close
            //
            this._close.AutoSize = true;
            this._close.Location = new System.Drawing.Point(168, 3);
            this._close.Name = "_close";
            this._close.Size = new System.Drawing.Size(75, 23);
            this._close.TabIndex = 2;
            this._close.Text = "Close";
            this._close.Click += new System.EventHandler(this._close_Click);
            //
            // SpinDialog
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(449, 381);
            this.Controls.Add(this.rootPanel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "SpinDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Rotate Continuously";
            this.rootPanel.ResumeLayout(false);
            this.rootPanel.PerformLayout();
            this.axisRow.ResumeLayout(false);
            this.axisRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._speed)).EndInit();
            this.buttonsRow.ResumeLayout(false);
            this.buttonsRow.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel rootPanel;
        private System.Windows.Forms.Label _title;
        private System.Windows.Forms.Label introLabel;
        private System.Windows.Forms.FlowLayoutPanel axisRow;
        private System.Windows.Forms.Label axisLabel;
        private System.Windows.Forms.RadioButton _x;
        private System.Windows.Forms.RadioButton _y;
        private System.Windows.Forms.RadioButton _z;
        private System.Windows.Forms.Label speedLabel;
        private System.Windows.Forms.NumericUpDown _speed;
        private System.Windows.Forms.Label _period;
        private System.Windows.Forms.Label _suggest;
        private System.Windows.Forms.Panel previewHost;
        private System.Windows.Forms.Label _status;
        private System.Windows.Forms.FlowLayoutPanel buttonsRow;
        private System.Windows.Forms.Button _apply;
        private System.Windows.Forms.Button _remove;
        private System.Windows.Forms.Button _close;
    }
}

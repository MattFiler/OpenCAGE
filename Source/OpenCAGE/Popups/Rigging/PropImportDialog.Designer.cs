namespace OpenCAGE.Popups.Rigging
{
    partial class PropImportDialog
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
            this._root = new System.Windows.Forms.FlowLayoutPanel();
            this._introLabel = new System.Windows.Forms.Label();
            this._fileRow = new System.Windows.Forms.FlowLayoutPanel();
            this._fileLabel = new System.Windows.Forms.Label();
            this._file = new System.Windows.Forms.TextBox();
            this._browse = new System.Windows.Forms.Button();
            this._nameRow = new System.Windows.Forms.FlowLayoutPanel();
            this._nameLabel = new System.Windows.Forms.Label();
            this._name = new System.Windows.Forms.TextBox();
            this._rateLabel = new System.Windows.Forms.Label();
            this._rate = new System.Windows.Forms.NumericUpDown();
            this._compositeRow = new System.Windows.Forms.FlowLayoutPanel();
            this._compositeLabel = new System.Windows.Forms.Label();
            this._composite = new System.Windows.Forms.TextBox();
            this._playRow = new System.Windows.Forms.FlowLayoutPanel();
            this._playLabel = new System.Windows.Forms.Label();
            this._play = new System.Windows.Forms.TextBox();
            this._loop = new System.Windows.Forms.CheckBox();
            this._place = new System.Windows.Forms.CheckBox();
            this._report = new System.Windows.Forms.TextBox();
            this._buttonRow = new System.Windows.Forms.FlowLayoutPanel();
            this._check = new System.Windows.Forms.Button();
            this._import = new System.Windows.Forms.Button();
            this._close = new System.Windows.Forms.Button();
            this._root.SuspendLayout();
            this._fileRow.SuspendLayout();
            this._nameRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._rate)).BeginInit();
            this._compositeRow.SuspendLayout();
            this._playRow.SuspendLayout();
            this._buttonRow.SuspendLayout();
            this.SuspendLayout();
            //
            // _root
            //
            this._root.AutoSize = true;
            this._root.Controls.Add(this._introLabel);
            this._root.Controls.Add(this._fileRow);
            this._root.Controls.Add(this._nameRow);
            this._root.Controls.Add(this._compositeRow);
            this._root.Controls.Add(this._playRow);
            this._root.Controls.Add(this._place);
            this._root.Controls.Add(this._report);
            this._root.Controls.Add(this._buttonRow);
            this._root.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this._root.Location = new System.Drawing.Point(0, 0);
            this._root.Name = "_root";
            this._root.Padding = new System.Windows.Forms.Padding(10);
            this._root.Size = new System.Drawing.Size(666, 494);
            this._root.TabIndex = 0;
            this._root.WrapContents = false;
            //
            // _introLabel
            //
            this._introLabel.AutoSize = true;
            this._introLabel.Location = new System.Drawing.Point(13, 10);
            this._introLabel.MaximumSize = new System.Drawing.Size(640, 0);
            this._introLabel.Name = "_introLabel";
            this._introLabel.Size = new System.Drawing.Size(630, 39);
            this._introLabel.TabIndex = 0;
            this._introLabel.Text = "An FBX or glTF whose parts are animated as objects (each part its own node, keyed), or skinned to an armature. The prop gets a rig and an animation set of its own in ANIMATION.PAK (shared by every level, written at once), and comes into the open level as a composite that plays a clip.";
            //
            // _fileRow
            //
            this._fileRow.AutoSize = true;
            this._fileRow.Controls.Add(this._fileLabel);
            this._fileRow.Controls.Add(this._file);
            this._fileRow.Controls.Add(this._browse);
            this._fileRow.Location = new System.Drawing.Point(13, 52);
            this._fileRow.Name = "_fileRow";
            this._fileRow.Size = new System.Drawing.Size(494, 29);
            this._fileRow.TabIndex = 1;
            this._fileRow.WrapContents = false;
            //
            // _fileLabel
            //
            this._fileLabel.AutoSize = true;
            this._fileLabel.Location = new System.Drawing.Point(3, 6);
            this._fileLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._fileLabel.Name = "_fileLabel";
            this._fileLabel.Size = new System.Drawing.Size(26, 13);
            this._fileLabel.TabIndex = 0;
            this._fileLabel.Text = "File:";
            //
            // _file
            //
            this._file.Location = new System.Drawing.Point(35, 3);
            this._file.Name = "_file";
            this._file.Size = new System.Drawing.Size(420, 20);
            this._file.TabIndex = 1;
            //
            // _browse
            //
            this._browse.Location = new System.Drawing.Point(461, 3);
            this._browse.Name = "_browse";
            this._browse.Size = new System.Drawing.Size(30, 23);
            this._browse.TabIndex = 2;
            this._browse.Text = "...";
            this._browse.Click += new System.EventHandler(this.Browse_Click);
            //
            // _nameRow
            //
            this._nameRow.AutoSize = true;
            this._nameRow.Controls.Add(this._nameLabel);
            this._nameRow.Controls.Add(this._name);
            this._nameRow.Controls.Add(this._rateLabel);
            this._nameRow.Controls.Add(this._rate);
            this._nameRow.Location = new System.Drawing.Point(13, 87);
            this._nameRow.Name = "_nameRow";
            this._nameRow.Size = new System.Drawing.Size(480, 26);
            this._nameRow.TabIndex = 2;
            this._nameRow.WrapContents = false;
            //
            // _nameLabel
            //
            this._nameLabel.AutoSize = true;
            this._nameLabel.Location = new System.Drawing.Point(3, 6);
            this._nameLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._nameLabel.Name = "_nameLabel";
            this._nameLabel.Size = new System.Drawing.Size(93, 13);
            this._nameLabel.TabIndex = 0;
            this._nameLabel.Text = "Rig and set name:";
            //
            // _name
            //
            this._name.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this._name.Location = new System.Drawing.Point(102, 3);
            this._name.Name = "_name";
            this._name.Size = new System.Drawing.Size(200, 20);
            this._name.TabIndex = 1;
            this._name.TextChanged += new System.EventHandler(this.Name_TextChanged);
            //
            // _rateLabel
            //
            this._rateLabel.AutoSize = true;
            this._rateLabel.Location = new System.Drawing.Point(308, 6);
            this._rateLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._rateLabel.Name = "_rateLabel";
            this._rateLabel.Size = new System.Drawing.Size(103, 13);
            this._rateLabel.TabIndex = 2;
            this._rateLabel.Text = "  frames per second:";
            //
            // _rate
            //
            this._rate.Location = new System.Drawing.Point(417, 3);
            this._rate.Maximum = new decimal(new int[] {
            240,
            0,
            0,
            0});
            this._rate.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this._rate.Name = "_rate";
            this._rate.Size = new System.Drawing.Size(60, 20);
            this._rate.TabIndex = 3;
            this._rate.Value = new decimal(new int[] {
            30,
            0,
            0,
            0});
            //
            // _compositeRow
            //
            this._compositeRow.AutoSize = true;
            this._compositeRow.Controls.Add(this._compositeLabel);
            this._compositeRow.Controls.Add(this._composite);
            this._compositeRow.Location = new System.Drawing.Point(13, 119);
            this._compositeRow.Name = "_compositeRow";
            this._compositeRow.Size = new System.Drawing.Size(331, 26);
            this._compositeRow.TabIndex = 3;
            this._compositeRow.WrapContents = false;
            //
            // _compositeLabel
            //
            this._compositeLabel.AutoSize = true;
            this._compositeLabel.Location = new System.Drawing.Point(3, 6);
            this._compositeLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._compositeLabel.Name = "_compositeLabel";
            this._compositeLabel.Size = new System.Drawing.Size(59, 13);
            this._compositeLabel.TabIndex = 0;
            this._compositeLabel.Text = "Composite:";
            //
            // _composite
            //
            this._composite.Location = new System.Drawing.Point(68, 3);
            this._composite.Name = "_composite";
            this._composite.Size = new System.Drawing.Size(260, 20);
            this._composite.TabIndex = 1;
            //
            // _playRow
            //
            this._playRow.AutoSize = true;
            this._playRow.Controls.Add(this._playLabel);
            this._playRow.Controls.Add(this._play);
            this._playRow.Controls.Add(this._loop);
            this._playRow.Location = new System.Drawing.Point(13, 151);
            this._playRow.Name = "_playRow";
            this._playRow.Size = new System.Drawing.Size(469, 26);
            this._playRow.TabIndex = 4;
            this._playRow.WrapContents = false;
            //
            // _playLabel
            //
            this._playLabel.AutoSize = true;
            this._playLabel.Location = new System.Drawing.Point(3, 6);
            this._playLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._playLabel.Name = "_playLabel";
            this._playLabel.Size = new System.Drawing.Size(233, 13);
            this._playLabel.TabIndex = 0;
            this._playLabel.Text = "Play from the start (clip, empty = the first, \'none\'):";
            //
            // _play
            //
            this._play.Location = new System.Drawing.Point(242, 3);
            this._play.Name = "_play";
            this._play.Size = new System.Drawing.Size(160, 20);
            this._play.TabIndex = 1;
            //
            // _loop
            //
            this._loop.AutoSize = true;
            this._loop.Checked = true;
            this._loop.CheckState = System.Windows.Forms.CheckState.Checked;
            this._loop.Location = new System.Drawing.Point(408, 3);
            this._loop.Name = "_loop";
            this._loop.Size = new System.Drawing.Size(58, 17);
            this._loop.TabIndex = 2;
            this._loop.Text = "Loop it";
            //
            // _place
            //
            this._place.AutoSize = true;
            this._place.Location = new System.Drawing.Point(13, 183);
            this._place.Name = "_place";
            this._place.Size = new System.Drawing.Size(173, 17);
            this._place.TabIndex = 5;
            this._place.Text = "Also place it at the level\'s origin";
            //
            // _report
            //
            this._report.Location = new System.Drawing.Point(13, 206);
            this._report.Multiline = true;
            this._report.Name = "_report";
            this._report.ReadOnly = true;
            this._report.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._report.Size = new System.Drawing.Size(640, 240);
            this._report.TabIndex = 6;
            //
            // _buttonRow
            //
            this._buttonRow.AutoSize = true;
            this._buttonRow.Controls.Add(this._check);
            this._buttonRow.Controls.Add(this._import);
            this._buttonRow.Controls.Add(this._close);
            this._buttonRow.Location = new System.Drawing.Point(13, 452);
            this._buttonRow.Name = "_buttonRow";
            this._buttonRow.Size = new System.Drawing.Size(243, 29);
            this._buttonRow.TabIndex = 7;
            this._buttonRow.WrapContents = false;
            //
            // _check
            //
            this._check.AutoSize = true;
            this._check.Location = new System.Drawing.Point(3, 3);
            this._check.Name = "_check";
            this._check.Size = new System.Drawing.Size(75, 23);
            this._check.TabIndex = 0;
            this._check.Text = "Check";
            this._check.Click += new System.EventHandler(this.Check_Click);
            //
            // _import
            //
            this._import.AutoSize = true;
            this._import.Location = new System.Drawing.Point(84, 3);
            this._import.Name = "_import";
            this._import.Size = new System.Drawing.Size(75, 23);
            this._import.TabIndex = 1;
            this._import.Text = "Import";
            this._import.Click += new System.EventHandler(this.Import_Click);
            //
            // _close
            //
            this._close.AutoSize = true;
            this._close.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._close.Location = new System.Drawing.Point(165, 3);
            this._close.Name = "_close";
            this._close.Size = new System.Drawing.Size(75, 23);
            this._close.TabIndex = 2;
            this._close.Text = "Close";
            //
            // PropImportDialog
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.CancelButton = this._close;
            this.ClientSize = new System.Drawing.Size(669, 497);
            this.Controls.Add(this._root);
            this.Name = "PropImportDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Import animated prop";
            this._root.ResumeLayout(false);
            this._root.PerformLayout();
            this._fileRow.ResumeLayout(false);
            this._fileRow.PerformLayout();
            this._nameRow.ResumeLayout(false);
            this._nameRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._rate)).EndInit();
            this._compositeRow.ResumeLayout(false);
            this._compositeRow.PerformLayout();
            this._playRow.ResumeLayout(false);
            this._playRow.PerformLayout();
            this._buttonRow.ResumeLayout(false);
            this._buttonRow.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel _root;
        private System.Windows.Forms.Label _introLabel;
        private System.Windows.Forms.FlowLayoutPanel _fileRow;
        private System.Windows.Forms.Label _fileLabel;
        private System.Windows.Forms.TextBox _file;
        private System.Windows.Forms.Button _browse;
        private System.Windows.Forms.FlowLayoutPanel _nameRow;
        private System.Windows.Forms.Label _nameLabel;
        private System.Windows.Forms.TextBox _name;
        private System.Windows.Forms.Label _rateLabel;
        private System.Windows.Forms.NumericUpDown _rate;
        private System.Windows.Forms.FlowLayoutPanel _compositeRow;
        private System.Windows.Forms.Label _compositeLabel;
        private System.Windows.Forms.TextBox _composite;
        private System.Windows.Forms.FlowLayoutPanel _playRow;
        private System.Windows.Forms.Label _playLabel;
        private System.Windows.Forms.TextBox _play;
        private System.Windows.Forms.CheckBox _loop;
        private System.Windows.Forms.CheckBox _place;
        private System.Windows.Forms.TextBox _report;
        private System.Windows.Forms.FlowLayoutPanel _buttonRow;
        private System.Windows.Forms.Button _check;
        private System.Windows.Forms.Button _import;
        private System.Windows.Forms.Button _close;
    }
}
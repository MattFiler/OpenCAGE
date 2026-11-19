namespace OpenCAGE.Popups.Characters
{
    partial class FirstPersonBodyDialog
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
            this._picture = new System.Windows.Forms.PictureBox();
            this._nameRow = new System.Windows.Forms.FlowLayoutPanel();
            this._nameLabel = new System.Windows.Forms.Label();
            this._name = new System.Windows.Forms.TextBox();
            this._rigLabel = new System.Windows.Forms.Label();
            this._rig = new System.Windows.Forms.TextBox();
            this._optionsRow = new System.Windows.Forms.FlowLayoutPanel();
            this._lenient = new System.Windows.Forms.CheckBox();
            this._keepTorso = new System.Windows.Forms.CheckBox();
            this._player = new System.Windows.Forms.CheckBox();
            this._report = new System.Windows.Forms.TextBox();
            this._buttonRow = new System.Windows.Forms.FlowLayoutPanel();
            this._preview = new System.Windows.Forms.Button();
            this._make = new System.Windows.Forms.Button();
            this._close = new System.Windows.Forms.Button();
            this._root.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._picture)).BeginInit();
            this._nameRow.SuspendLayout();
            this._optionsRow.SuspendLayout();
            this._buttonRow.SuspendLayout();
            this.SuspendLayout();
            //
            // _root
            //
            this._root.AutoSize = true;
            this._root.Controls.Add(this._introLabel);
            this._root.Controls.Add(this._picture);
            this._root.Controls.Add(this._nameRow);
            this._root.Controls.Add(this._optionsRow);
            this._root.Controls.Add(this._report);
            this._root.Controls.Add(this._buttonRow);
            this._root.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this._root.Location = new System.Drawing.Point(0, 0);
            this._root.Name = "_root";
            this._root.Padding = new System.Windows.Forms.Padding(10);
            this._root.Size = new System.Drawing.Size(926, 924);
            this._root.TabIndex = 0;
            this._root.WrapContents = false;
            //
            // _introLabel
            //
            this._introLabel.AutoSize = true;
            this._introLabel.Location = new System.Drawing.Point(13, 10);
            this._introLabel.MaximumSize = new System.Drawing.Size(900, 0);
            this._introLabel.Name = "_introLabel";
            this._introLabel.Size = new System.Drawing.Size(889, 26);
            this._introLabel.TabIndex = 0;
            this._introLabel.Text = "A copy of the body without what the player\'s camera (the HEAD bone, green) would see from inside: the head, neck and clavicles, and the upper torso except a front panel - as the game\'s own first-person bodies are cut. Red is what goes. The player\'s rig is FEMALEFP: rebind a body onto it first (Model import > Rebind onto a game skeleton).";
            //
            // _picture
            //
            this._picture.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(28)))), ((int)(((byte)(30)))), ((int)(((byte)(34)))));
            this._picture.Location = new System.Drawing.Point(13, 39);
            this._picture.Name = "_picture";
            this._picture.Size = new System.Drawing.Size(900, 620);
            this._picture.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this._picture.TabIndex = 1;
            this._picture.TabStop = false;
            //
            // _nameRow
            //
            this._nameRow.AutoSize = true;
            this._nameRow.Controls.Add(this._nameLabel);
            this._nameRow.Controls.Add(this._name);
            this._nameRow.Controls.Add(this._rigLabel);
            this._nameRow.Controls.Add(this._rig);
            this._nameRow.Location = new System.Drawing.Point(13, 665);
            this._nameRow.Name = "_nameRow";
            this._nameRow.Size = new System.Drawing.Size(711, 26);
            this._nameRow.TabIndex = 2;
            this._nameRow.WrapContents = false;
            //
            // _nameLabel
            //
            this._nameLabel.AutoSize = true;
            this._nameLabel.Location = new System.Drawing.Point(3, 6);
            this._nameLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._nameLabel.Name = "_nameLabel";
            this._nameLabel.Size = new System.Drawing.Size(98, 13);
            this._nameLabel.TabIndex = 0;
            this._nameLabel.Text = "New display model:";
            //
            // _name
            //
            this._name.Location = new System.Drawing.Point(107, 3);
            this._name.Name = "_name";
            this._name.Size = new System.Drawing.Size(300, 20);
            this._name.TabIndex = 1;
            //
            // _rigLabel
            //
            this._rigLabel.AutoSize = true;
            this._rigLabel.Location = new System.Drawing.Point(413, 6);
            this._rigLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._rigLabel.Name = "_rigLabel";
            this._rigLabel.Size = new System.Drawing.Size(169, 13);
            this._rigLabel.TabIndex = 2;
            this._rigLabel.Text = "  Rig (empty = the display model\'s):";
            //
            // _rig
            //
            this._rig.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this._rig.Location = new System.Drawing.Point(588, 3);
            this._rig.Name = "_rig";
            this._rig.Size = new System.Drawing.Size(120, 20);
            this._rig.TabIndex = 3;
            //
            // _optionsRow
            //
            this._optionsRow.AutoSize = true;
            this._optionsRow.Controls.Add(this._lenient);
            this._optionsRow.Controls.Add(this._keepTorso);
            this._optionsRow.Controls.Add(this._player);
            this._optionsRow.Location = new System.Drawing.Point(13, 697);
            this._optionsRow.Name = "_optionsRow";
            this._optionsRow.Size = new System.Drawing.Size(699, 23);
            this._optionsRow.TabIndex = 3;
            this._optionsRow.WrapContents = false;
            //
            // _lenient
            //
            this._lenient.AutoSize = true;
            this._lenient.Location = new System.Drawing.Point(3, 3);
            this._lenient.Name = "_lenient";
            this._lenient.Size = new System.Drawing.Size(315, 17);
            this._lenient.TabIndex = 0;
            this._lenient.Text = "Keep triangles with one corner in the cut (low-polygon bodies)";
            this._lenient.CheckedChanged += new System.EventHandler(this.Cut_CheckedChanged);
            //
            // _keepTorso
            //
            this._keepTorso.AutoSize = true;
            this._keepTorso.Location = new System.Drawing.Point(324, 3);
            this._keepTorso.Name = "_keepTorso";
            this._keepTorso.Size = new System.Drawing.Size(126, 17);
            this._keepTorso.TabIndex = 1;
            this._keepTorso.Text = "Keep the whole torso";
            this._keepTorso.CheckedChanged += new System.EventHandler(this.Cut_CheckedChanged);
            //
            // _player
            //
            this._player.AutoSize = true;
            this._player.Checked = true;
            this._player.CheckState = System.Windows.Forms.CheckState.Checked;
            this._player.Location = new System.Drawing.Point(456, 3);
            this._player.Name = "_player";
            this._player.Size = new System.Drawing.Size(240, 17);
            this._player.TabIndex = 2;
            this._player.Text = "Make it the player\'s model (Global\'s FlightSuit)";
            //
            // _report
            //
            this._report.Location = new System.Drawing.Point(13, 726);
            this._report.Multiline = true;
            this._report.Name = "_report";
            this._report.ReadOnly = true;
            this._report.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._report.Size = new System.Drawing.Size(900, 150);
            this._report.TabIndex = 4;
            //
            // _buttonRow
            //
            this._buttonRow.AutoSize = true;
            this._buttonRow.Controls.Add(this._preview);
            this._buttonRow.Controls.Add(this._make);
            this._buttonRow.Controls.Add(this._close);
            this._buttonRow.Location = new System.Drawing.Point(13, 882);
            this._buttonRow.Name = "_buttonRow";
            this._buttonRow.Size = new System.Drawing.Size(243, 29);
            this._buttonRow.TabIndex = 5;
            this._buttonRow.WrapContents = false;
            //
            // _preview
            //
            this._preview.AutoSize = true;
            this._preview.Location = new System.Drawing.Point(3, 3);
            this._preview.Name = "_preview";
            this._preview.Size = new System.Drawing.Size(75, 23);
            this._preview.TabIndex = 0;
            this._preview.Text = "Preview";
            this._preview.Click += new System.EventHandler(this.Preview_Click);
            //
            // _make
            //
            this._make.AutoSize = true;
            this._make.Location = new System.Drawing.Point(84, 3);
            this._make.Name = "_make";
            this._make.Size = new System.Drawing.Size(75, 23);
            this._make.TabIndex = 1;
            this._make.Text = "Make";
            this._make.Click += new System.EventHandler(this.Make_Click);
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
            // FirstPersonBodyDialog
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.CancelButton = this._close;
            this.ClientSize = new System.Drawing.Size(929, 927);
            this.Controls.Add(this._root);
            this.Name = "FirstPersonBodyDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Make a first-person body";
            this.Shown += new System.EventHandler(this.FirstPersonBodyDialog_Shown);
            this._root.ResumeLayout(false);
            this._root.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._picture)).EndInit();
            this._nameRow.ResumeLayout(false);
            this._nameRow.PerformLayout();
            this._optionsRow.ResumeLayout(false);
            this._optionsRow.PerformLayout();
            this._buttonRow.ResumeLayout(false);
            this._buttonRow.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel _root;
        private System.Windows.Forms.Label _introLabel;
        private System.Windows.Forms.PictureBox _picture;
        private System.Windows.Forms.FlowLayoutPanel _nameRow;
        private System.Windows.Forms.Label _nameLabel;
        private System.Windows.Forms.TextBox _name;
        private System.Windows.Forms.Label _rigLabel;
        private System.Windows.Forms.TextBox _rig;
        private System.Windows.Forms.FlowLayoutPanel _optionsRow;
        private System.Windows.Forms.CheckBox _lenient;
        private System.Windows.Forms.CheckBox _keepTorso;
        private System.Windows.Forms.CheckBox _player;
        private System.Windows.Forms.TextBox _report;
        private System.Windows.Forms.FlowLayoutPanel _buttonRow;
        private System.Windows.Forms.Button _preview;
        private System.Windows.Forms.Button _make;
        private System.Windows.Forms.Button _close;
    }
}
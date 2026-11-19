namespace OpenCAGE.Breakables
{
    partial class FractureWindow
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
            this.components = new System.ComponentModel.Container();
            this._root = new System.Windows.Forms.FlowLayoutPanel();
            this._title = new System.Windows.Forms.Label();
            this._intro = new System.Windows.Forms.Label();
            this._cutRow = new System.Windows.Forms.FlowLayoutPanel();
            this._piecesLabel = new System.Windows.Forms.Label();
            this._pieces = new System.Windows.Forms.NumericUpDown();
            this._cutLabel = new System.Windows.Forms.Label();
            this._seed = new System.Windows.Forms.NumericUpDown();
            this._reseed = new System.Windows.Forms.Button();
            this._cutFacesLabel = new System.Windows.Forms.Label();
            this._interior = new System.Windows.Forms.ComboBox();
            this._skin = new System.Windows.Forms.CheckBox();
            this._massRow = new System.Windows.Forms.FlowLayoutPanel();
            this._autoMass = new System.Windows.Forms.CheckBox();
            this._mass = new System.Windows.Forms.NumericUpDown();
            this._massLabel = new System.Windows.Forms.Label();
            this._kick = new System.Windows.Forms.NumericUpDown();
            this._kickUnitLabel = new System.Windows.Forms.Label();
            this._damageRow = new System.Windows.Forms.FlowLayoutPanel();
            this._damage = new System.Windows.Forms.CheckBox();
            this._threshold = new System.Windows.Forms.NumericUpDown();
            this._monitorRow = new System.Windows.Forms.FlowLayoutPanel();
            this._monitor = new System.Windows.Forms.CheckBox();
            this._despawnLabel = new System.Windows.Forms.Label();
            this._despawn = new System.Windows.Forms.NumericUpDown();
            this._despawnUnitLabel = new System.Windows.Forms.Label();
            this._preview = new System.Windows.Forms.PictureBox();
            this._explodeRow = new System.Windows.Forms.FlowLayoutPanel();
            this._explodeLabel = new System.Windows.Forms.Label();
            this._explode = new System.Windows.Forms.TrackBar();
            this._stats = new System.Windows.Forms.Label();
            this._status = new System.Windows.Forms.Label();
            this._buttonsRow = new System.Windows.Forms.FlowLayoutPanel();
            this._make = new System.Windows.Forms.Button();
            this._close = new System.Windows.Forms.Button();
            this._recut = new System.Windows.Forms.Timer(this.components);
            this._root.SuspendLayout();
            this._cutRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._pieces)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._seed)).BeginInit();
            this._massRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._mass)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._kick)).BeginInit();
            this._damageRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._threshold)).BeginInit();
            this._monitorRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._despawn)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._preview)).BeginInit();
            this._explodeRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._explode)).BeginInit();
            this._buttonsRow.SuspendLayout();
            this.SuspendLayout();
            //
            // _root
            //
            this._root.AutoSize = true;
            this._root.Controls.Add(this._title);
            this._root.Controls.Add(this._intro);
            this._root.Controls.Add(this._cutRow);
            this._root.Controls.Add(this._massRow);
            this._root.Controls.Add(this._damageRow);
            this._root.Controls.Add(this._monitorRow);
            this._root.Controls.Add(this._preview);
            this._root.Controls.Add(this._explodeRow);
            this._root.Controls.Add(this._stats);
            this._root.Controls.Add(this._status);
            this._root.Controls.Add(this._buttonsRow);
            this._root.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this._root.Location = new System.Drawing.Point(0, 0);
            this._root.Name = "_root";
            this._root.Padding = new System.Windows.Forms.Padding(10);
            this._root.TabIndex = 0;
            this._root.WrapContents = false;
            //
            // _title
            //
            this._title.AutoSize = true;
            this._title.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
            this._title.MaximumSize = new System.Drawing.Size(560, 0);
            this._title.Name = "_title";
            this._title.TabIndex = 0;
            //
            // _intro
            //
            this._intro.AutoSize = true;
            this._intro.MaximumSize = new System.Drawing.Size(560, 0);
            this._intro.Name = "_intro";
            this._intro.TabIndex = 1;
            this._intro.Text = "The model is cut into pieces, each a physics asset of its own, spawned where it was when the model breaks: when it is shot or hit, or when a script calls its Break pin (Broken fires after). It goes into a composite of its own, placed where it was.";
            //
            // _cutRow
            //
            this._cutRow.AutoSize = true;
            this._cutRow.Controls.Add(this._piecesLabel);
            this._cutRow.Controls.Add(this._pieces);
            this._cutRow.Controls.Add(this._cutLabel);
            this._cutRow.Controls.Add(this._seed);
            this._cutRow.Controls.Add(this._reseed);
            this._cutRow.Controls.Add(this._cutFacesLabel);
            this._cutRow.Controls.Add(this._interior);
            this._cutRow.Controls.Add(this._skin);
            this._cutRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this._cutRow.Name = "_cutRow";
            this._cutRow.TabIndex = 2;
            this._cutRow.WrapContents = false;
            //
            // _piecesLabel
            //
            this._piecesLabel.AutoSize = true;
            this._piecesLabel.Name = "_piecesLabel";
            this._piecesLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this._piecesLabel.TabIndex = 0;
            this._piecesLabel.Text = "Pieces:";
            //
            // _pieces
            //
            this._pieces.Maximum = new decimal(new int[] {
            16,
            0,
            0,
            0});
            this._pieces.Minimum = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this._pieces.Name = "_pieces";
            this._pieces.Size = new System.Drawing.Size(50, 20);
            this._pieces.TabIndex = 1;
            this._pieces.Value = new decimal(new int[] {
            6,
            0,
            0,
            0});
            this._pieces.ValueChanged += new System.EventHandler(this._option_Changed);
            //
            // _cutLabel
            //
            this._cutLabel.AutoSize = true;
            this._cutLabel.Name = "_cutLabel";
            this._cutLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this._cutLabel.TabIndex = 2;
            this._cutLabel.Text = "  Cut:";
            //
            // _seed
            //
            this._seed.Maximum = new decimal(new int[] {
            99999,
            0,
            0,
            0});
            this._seed.Name = "_seed";
            this._seed.Size = new System.Drawing.Size(65, 20);
            this._seed.TabIndex = 3;
            this._seed.Value = new decimal(new int[] {
            7,
            0,
            0,
            0});
            this._seed.ValueChanged += new System.EventHandler(this._option_Changed);
            //
            // _reseed
            //
            this._reseed.AutoSize = true;
            this._reseed.Name = "_reseed";
            this._reseed.TabIndex = 4;
            this._reseed.Text = "Another Way";
            this._reseed.Click += new System.EventHandler(this._reseed_Click);
            //
            // _cutFacesLabel
            //
            this._cutFacesLabel.AutoSize = true;
            this._cutFacesLabel.Name = "_cutFacesLabel";
            this._cutFacesLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this._cutFacesLabel.TabIndex = 5;
            this._cutFacesLabel.Text = "  Cut faces:";
            //
            // _interior
            //
            this._interior.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._interior.Items.AddRange(new object[] {
            "Dark inside",
            "Texture inside"});
            this._interior.Name = "_interior";
            this._interior.Size = new System.Drawing.Size(95, 21);
            this._interior.TabIndex = 6;
            //
            // _skin
            //
            this._skin.AutoSize = true;
            this._skin.Checked = true;
            this._skin.CheckState = System.Windows.Forms.CheckState.Checked;
            this._skin.Name = "_skin";
            this._skin.TabIndex = 7;
            this._skin.Text = "Inner skin";
            this._skin.CheckedChanged += new System.EventHandler(this._option_Changed);
            //
            // _massRow
            //
            this._massRow.AutoSize = true;
            this._massRow.Controls.Add(this._autoMass);
            this._massRow.Controls.Add(this._mass);
            this._massRow.Controls.Add(this._massLabel);
            this._massRow.Controls.Add(this._kick);
            this._massRow.Controls.Add(this._kickUnitLabel);
            this._massRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this._massRow.Name = "_massRow";
            this._massRow.TabIndex = 3;
            this._massRow.WrapContents = false;
            //
            // _autoMass
            //
            this._autoMass.AutoSize = true;
            this._autoMass.Checked = true;
            this._autoMass.CheckState = System.Windows.Forms.CheckState.Checked;
            this._autoMass.Name = "_autoMass";
            this._autoMass.TabIndex = 0;
            this._autoMass.Text = "Mass from size";
            this._autoMass.CheckedChanged += new System.EventHandler(this._autoMass_CheckedChanged);
            //
            // _mass
            //
            this._mass.DecimalPlaces = 2;
            this._mass.Enabled = false;
            this._mass.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this._mass.Minimum = new decimal(new int[] {
            5,
            0,
            0,
            131072});
            this._mass.Name = "_mass";
            this._mass.Size = new System.Drawing.Size(75, 20);
            this._mass.TabIndex = 1;
            this._mass.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this._mass.ValueChanged += new System.EventHandler(this._option_Changed);
            //
            // _massLabel
            //
            this._massLabel.AutoSize = true;
            this._massLabel.Name = "_massLabel";
            this._massLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this._massLabel.TabIndex = 2;
            this._massLabel.Text = "kg in all    Thrown out at";
            //
            // _kick
            //
            this._kick.DecimalPlaces = 1;
            this._kick.Increment = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this._kick.Maximum = new decimal(new int[] {
            30,
            0,
            0,
            0});
            this._kick.Name = "_kick";
            this._kick.Size = new System.Drawing.Size(55, 20);
            this._kick.TabIndex = 3;
            this._kick.Value = new decimal(new int[] {
            25,
            0,
            0,
            65536});
            this._kick.ValueChanged += new System.EventHandler(this._option_Changed);
            //
            // _kickUnitLabel
            //
            this._kickUnitLabel.AutoSize = true;
            this._kickUnitLabel.Name = "_kickUnitLabel";
            this._kickUnitLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this._kickUnitLabel.TabIndex = 4;
            this._kickUnitLabel.Text = "m/s";
            //
            // _damageRow
            //
            this._damageRow.AutoSize = true;
            this._damageRow.Controls.Add(this._damage);
            this._damageRow.Controls.Add(this._threshold);
            this._damageRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this._damageRow.Name = "_damageRow";
            this._damageRow.TabIndex = 4;
            this._damageRow.WrapContents = false;
            //
            // _damage
            //
            this._damage.AutoSize = true;
            this._damage.Checked = true;
            this._damage.CheckState = System.Windows.Forms.CheckState.Checked;
            this._damage.Name = "_damage";
            this._damage.TabIndex = 0;
            this._damage.Text = "Breaks when shot or hit, damage at least";
            this._damage.CheckedChanged += new System.EventHandler(this._damage_CheckedChanged);
            //
            // _threshold
            //
            this._threshold.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this._threshold.Name = "_threshold";
            this._threshold.Size = new System.Drawing.Size(60, 20);
            this._threshold.TabIndex = 1;
            this._threshold.Value = new decimal(new int[] {
            20,
            0,
            0,
            0});
            //
            // _monitorRow
            //
            this._monitorRow.AutoSize = true;
            this._monitorRow.Controls.Add(this._monitor);
            this._monitorRow.Controls.Add(this._despawnLabel);
            this._monitorRow.Controls.Add(this._despawn);
            this._monitorRow.Controls.Add(this._despawnUnitLabel);
            this._monitorRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this._monitorRow.Name = "_monitorRow";
            this._monitorRow.TabIndex = 5;
            this._monitorRow.WrapContents = false;
            //
            // _monitor
            //
            this._monitor.AutoSize = true;
            this._monitor.Checked = true;
            this._monitor.CheckState = System.Windows.Forms.CheckState.Checked;
            this._monitor.Name = "_monitor";
            this._monitor.TabIndex = 0;
            this._monitor.Text = "Also pistol / shotgun hits anywhere round it";
            //
            // _despawnLabel
            //
            this._despawnLabel.AutoSize = true;
            this._despawnLabel.Name = "_despawnLabel";
            this._despawnLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this._despawnLabel.TabIndex = 1;
            this._despawnLabel.Text = "   Pieces go after";
            //
            // _despawn
            //
            this._despawn.Maximum = new decimal(new int[] {
            3600,
            0,
            0,
            0});
            this._despawn.Name = "_despawn";
            this._despawn.Size = new System.Drawing.Size(60, 20);
            this._despawn.TabIndex = 2;
            //
            // _despawnUnitLabel
            //
            this._despawnUnitLabel.AutoSize = true;
            this._despawnUnitLabel.Name = "_despawnUnitLabel";
            this._despawnUnitLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this._despawnUnitLabel.TabIndex = 3;
            this._despawnUnitLabel.Text = "s (0 = they stay)";
            //
            // _preview
            //
            this._preview.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(34)))), ((int)(((byte)(38)))));
            this._preview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this._preview.Name = "_preview";
            this._preview.Size = new System.Drawing.Size(560, 320);
            this._preview.TabIndex = 6;
            //
            // _explodeRow
            //
            this._explodeRow.AutoSize = true;
            this._explodeRow.Controls.Add(this._explodeLabel);
            this._explodeRow.Controls.Add(this._explode);
            this._explodeRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this._explodeRow.Name = "_explodeRow";
            this._explodeRow.TabIndex = 7;
            this._explodeRow.WrapContents = false;
            //
            // _explodeLabel
            //
            this._explodeLabel.AutoSize = true;
            this._explodeLabel.Name = "_explodeLabel";
            this._explodeLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this._explodeLabel.TabIndex = 0;
            this._explodeLabel.Text = "Pushed apart:";
            //
            // _explode
            //
            this._explode.Maximum = 100;
            this._explode.Name = "_explode";
            this._explode.Size = new System.Drawing.Size(160, 45);
            this._explode.TabIndex = 1;
            this._explode.TickStyle = System.Windows.Forms.TickStyle.None;
            this._explode.Value = 35;
            this._explode.ValueChanged += new System.EventHandler(this._explode_ValueChanged);
            //
            // _stats
            //
            this._stats.AutoSize = true;
            this._stats.MaximumSize = new System.Drawing.Size(560, 0);
            this._stats.Name = "_stats";
            this._stats.TabIndex = 8;
            //
            // _status
            //
            this._status.AutoSize = true;
            this._status.MaximumSize = new System.Drawing.Size(560, 0);
            this._status.Name = "_status";
            this._status.TabIndex = 9;
            //
            // _buttonsRow
            //
            this._buttonsRow.AutoSize = true;
            this._buttonsRow.Controls.Add(this._make);
            this._buttonsRow.Controls.Add(this._close);
            this._buttonsRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this._buttonsRow.Name = "_buttonsRow";
            this._buttonsRow.TabIndex = 10;
            this._buttonsRow.WrapContents = false;
            //
            // _make
            //
            this._make.AutoSize = true;
            this._make.Name = "_make";
            this._make.TabIndex = 0;
            this._make.Text = "Make Breakable";
            this._make.Click += new System.EventHandler(this._make_Click);
            //
            // _close
            //
            this._close.AutoSize = true;
            this._close.Name = "_close";
            this._close.TabIndex = 1;
            this._close.Text = "Close";
            this._close.Click += new System.EventHandler(this._close_Click);
            //
            // _recut
            //
            this._recut.Interval = 350;
            this._recut.Tick += new System.EventHandler(this._recut_Tick);
            //
            // FractureWindow
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(590, 640);
            this.Controls.Add(this._root);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "FractureWindow";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Make Breakable";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.FractureWindow_FormClosed);
            this.Shown += new System.EventHandler(this.FractureWindow_Shown);
            this._root.ResumeLayout(false);
            this._root.PerformLayout();
            this._cutRow.ResumeLayout(false);
            this._cutRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._pieces)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._seed)).EndInit();
            this._massRow.ResumeLayout(false);
            this._massRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._mass)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._kick)).EndInit();
            this._damageRow.ResumeLayout(false);
            this._damageRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._threshold)).EndInit();
            this._monitorRow.ResumeLayout(false);
            this._monitorRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._despawn)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._preview)).EndInit();
            this._explodeRow.ResumeLayout(false);
            this._explodeRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._explode)).EndInit();
            this._buttonsRow.ResumeLayout(false);
            this._buttonsRow.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel _root;
        private System.Windows.Forms.Label _title;
        private System.Windows.Forms.Label _intro;
        private System.Windows.Forms.FlowLayoutPanel _cutRow;
        private System.Windows.Forms.Label _piecesLabel;
        private System.Windows.Forms.NumericUpDown _pieces;
        private System.Windows.Forms.Label _cutLabel;
        private System.Windows.Forms.NumericUpDown _seed;
        private System.Windows.Forms.Button _reseed;
        private System.Windows.Forms.Label _cutFacesLabel;
        private System.Windows.Forms.ComboBox _interior;
        private System.Windows.Forms.CheckBox _skin;
        private System.Windows.Forms.FlowLayoutPanel _massRow;
        private System.Windows.Forms.CheckBox _autoMass;
        private System.Windows.Forms.NumericUpDown _mass;
        private System.Windows.Forms.Label _massLabel;
        private System.Windows.Forms.NumericUpDown _kick;
        private System.Windows.Forms.Label _kickUnitLabel;
        private System.Windows.Forms.FlowLayoutPanel _damageRow;
        private System.Windows.Forms.CheckBox _damage;
        private System.Windows.Forms.NumericUpDown _threshold;
        private System.Windows.Forms.FlowLayoutPanel _monitorRow;
        private System.Windows.Forms.CheckBox _monitor;
        private System.Windows.Forms.Label _despawnLabel;
        private System.Windows.Forms.NumericUpDown _despawn;
        private System.Windows.Forms.Label _despawnUnitLabel;
        private System.Windows.Forms.PictureBox _preview;
        private System.Windows.Forms.FlowLayoutPanel _explodeRow;
        private System.Windows.Forms.Label _explodeLabel;
        private System.Windows.Forms.TrackBar _explode;
        private System.Windows.Forms.Label _stats;
        private System.Windows.Forms.Label _status;
        private System.Windows.Forms.FlowLayoutPanel _buttonsRow;
        private System.Windows.Forms.Button _make;
        private System.Windows.Forms.Button _close;
        private System.Windows.Forms.Timer _recut;
    }
}

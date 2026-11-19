namespace OpenCAGE
{
    partial class LevelSoundTuneDialog
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
            this.info = new System.Windows.Forms.GroupBox();
            this.about = new System.Windows.Forms.TextBox();
            this.buttons = new System.Windows.Forms.FlowLayoutPanel();
            this.closeBtn = new System.Windows.Forms.Button();
            this.revertBtn = new System.Windows.Forms.Button();
            this.applyBtn = new System.Windows.Forms.Button();
            this.grid = new System.Windows.Forms.TableLayoutPanel();
            this.volumeLabel = new System.Windows.Forms.Label();
            this.volume = new System.Windows.Forms.NumericUpDown();
            this.volumeOn = new System.Windows.Forms.CheckBox();
            this.volumeShip = new System.Windows.Forms.Label();
            this.pitchLabel = new System.Windows.Forms.Label();
            this.pitch = new System.Windows.Forms.NumericUpDown();
            this.pitchOn = new System.Windows.Forms.CheckBox();
            this.pitchShip = new System.Windows.Forms.Label();
            this.delayLabel = new System.Windows.Forms.Label();
            this.delay = new System.Windows.Forms.NumericUpDown();
            this.delayOn = new System.Windows.Forms.CheckBox();
            this.delayShip = new System.Windows.Forms.Label();
            this.fadeInLabel = new System.Windows.Forms.Label();
            this.fadeIn = new System.Windows.Forms.NumericUpDown();
            this.fadeInOn = new System.Windows.Forms.CheckBox();
            this.fadeInShip = new System.Windows.Forms.Label();
            this.fadeOutLabel = new System.Windows.Forms.Label();
            this.fadeOut = new System.Windows.Forms.NumericUpDown();
            this.fadeOutOn = new System.Windows.Forms.CheckBox();
            this.fadeOutShip = new System.Windows.Forms.Label();
            this.stopsLabel = new System.Windows.Forms.Label();
            this.stops = new System.Windows.Forms.ComboBox();
            this.stopsOn = new System.Windows.Forms.CheckBox();
            this.stopsShip = new System.Windows.Forms.Label();
            this.loopLabel = new System.Windows.Forms.Label();
            this.loopRow = new System.Windows.Forms.FlowLayoutPanel();
            this.loop = new System.Windows.Forms.ComboBox();
            this.loopCount = new System.Windows.Forms.NumericUpDown();
            this.loopOn = new System.Windows.Forms.CheckBox();
            this.loopShip = new System.Windows.Forms.Label();
            this.reachLabel = new System.Windows.Forms.Label();
            this.reach = new System.Windows.Forms.NumericUpDown();
            this.reachOn = new System.Windows.Forms.CheckBox();
            this.reachShip = new System.Windows.Forms.Label();
            this.eventRow = new System.Windows.Forms.TableLayoutPanel();
            this.eventLabel = new System.Windows.Forms.Label();
            this.eventName = new System.Windows.Forms.TextBox();
            this.readBtn = new System.Windows.Forms.Button();
            this.tips = new System.Windows.Forms.ToolTip(this.components);
            this.info.SuspendLayout();
            this.buttons.SuspendLayout();
            this.grid.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.volume)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pitch)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.delay)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.fadeIn)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.fadeOut)).BeginInit();
            this.loopRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.loopCount)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.reach)).BeginInit();
            this.eventRow.SuspendLayout();
            this.SuspendLayout();
            //
            // info
            //
            this.info.Controls.Add(this.about);
            this.info.Dock = System.Windows.Forms.DockStyle.Fill;
            this.info.Location = new System.Drawing.Point(0, 254);
            this.info.Name = "info";
            this.info.Padding = new System.Windows.Forms.Padding(6);
            this.info.Size = new System.Drawing.Size(760, 308);
            this.info.TabIndex = 0;
            this.info.TabStop = false;
            this.info.Text = "The event on this level";
            //
            // about
            //
            this.about.Dock = System.Windows.Forms.DockStyle.Fill;
            this.about.Location = new System.Drawing.Point(6, 19);
            this.about.Multiline = true;
            this.about.Name = "about";
            this.about.ReadOnly = true;
            this.about.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.about.Size = new System.Drawing.Size(748, 283);
            this.about.TabIndex = 0;
            //
            // buttons
            //
            this.buttons.Controls.Add(this.closeBtn);
            this.buttons.Controls.Add(this.revertBtn);
            this.buttons.Controls.Add(this.applyBtn);
            this.buttons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.buttons.Location = new System.Drawing.Point(0, 562);
            this.buttons.Name = "buttons";
            this.buttons.Padding = new System.Windows.Forms.Padding(4);
            this.buttons.Size = new System.Drawing.Size(760, 38);
            this.buttons.TabIndex = 1;
            //
            // closeBtn
            //
            this.closeBtn.Location = new System.Drawing.Point(659, 7);
            this.closeBtn.Name = "closeBtn";
            this.closeBtn.Size = new System.Drawing.Size(90, 26);
            this.closeBtn.TabIndex = 0;
            this.closeBtn.Text = "Close";
            this.closeBtn.UseVisualStyleBackColor = true;
            this.closeBtn.Click += new System.EventHandler(this.closeBtn_Click);
            //
            // revertBtn
            //
            this.revertBtn.Enabled = false;
            this.revertBtn.Location = new System.Drawing.Point(563, 7);
            this.revertBtn.Name = "revertBtn";
            this.revertBtn.Size = new System.Drawing.Size(90, 26);
            this.revertBtn.TabIndex = 1;
            this.revertBtn.Text = "Put Back";
            this.tips.SetToolTip(this.revertBtn, "Give the event (and its Stop_ event) the base game's settings again on this level.");
            this.revertBtn.UseVisualStyleBackColor = true;
            this.revertBtn.Click += new System.EventHandler(this.revertBtn_Click);
            //
            // applyBtn
            //
            this.applyBtn.Enabled = false;
            this.applyBtn.Location = new System.Drawing.Point(467, 7);
            this.applyBtn.Name = "applyBtn";
            this.applyBtn.Size = new System.Drawing.Size(90, 26);
            this.applyBtn.TabIndex = 2;
            this.applyBtn.Text = "Apply";
            this.tips.SetToolTip(this.applyBtn, "Write the ticked settings into the level's sound package (copying in the soundbanks that carry the event the first time).");
            this.applyBtn.UseVisualStyleBackColor = true;
            this.applyBtn.Click += new System.EventHandler(this.applyBtn_Click);
            //
            // grid
            //
            this.grid.AutoSize = true;
            this.grid.ColumnCount = 4;
            this.grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.grid.Controls.Add(this.volumeLabel, 0, 0);
            this.grid.Controls.Add(this.volume, 1, 0);
            this.grid.Controls.Add(this.volumeOn, 2, 0);
            this.grid.Controls.Add(this.volumeShip, 3, 0);
            this.grid.Controls.Add(this.pitchLabel, 0, 1);
            this.grid.Controls.Add(this.pitch, 1, 1);
            this.grid.Controls.Add(this.pitchOn, 2, 1);
            this.grid.Controls.Add(this.pitchShip, 3, 1);
            this.grid.Controls.Add(this.delayLabel, 0, 2);
            this.grid.Controls.Add(this.delay, 1, 2);
            this.grid.Controls.Add(this.delayOn, 2, 2);
            this.grid.Controls.Add(this.delayShip, 3, 2);
            this.grid.Controls.Add(this.fadeInLabel, 0, 3);
            this.grid.Controls.Add(this.fadeIn, 1, 3);
            this.grid.Controls.Add(this.fadeInOn, 2, 3);
            this.grid.Controls.Add(this.fadeInShip, 3, 3);
            this.grid.Controls.Add(this.fadeOutLabel, 0, 4);
            this.grid.Controls.Add(this.fadeOut, 1, 4);
            this.grid.Controls.Add(this.fadeOutOn, 2, 4);
            this.grid.Controls.Add(this.fadeOutShip, 3, 4);
            this.grid.Controls.Add(this.stopsLabel, 0, 5);
            this.grid.Controls.Add(this.stops, 1, 5);
            this.grid.Controls.Add(this.stopsOn, 2, 5);
            this.grid.Controls.Add(this.stopsShip, 3, 5);
            this.grid.Controls.Add(this.loopLabel, 0, 6);
            this.grid.Controls.Add(this.loopRow, 1, 6);
            this.grid.Controls.Add(this.loopOn, 2, 6);
            this.grid.Controls.Add(this.loopShip, 3, 6);
            this.grid.Controls.Add(this.reachLabel, 0, 7);
            this.grid.Controls.Add(this.reach, 1, 7);
            this.grid.Controls.Add(this.reachOn, 2, 7);
            this.grid.Controls.Add(this.reachShip, 3, 7);
            this.grid.Dock = System.Windows.Forms.DockStyle.Top;
            this.grid.Location = new System.Drawing.Point(0, 32);
            this.grid.Name = "grid";
            this.grid.Padding = new System.Windows.Forms.Padding(8, 6, 8, 6);
            this.grid.RowCount = 8;
            this.grid.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.grid.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.grid.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.grid.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.grid.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.grid.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.grid.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.grid.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.grid.Size = new System.Drawing.Size(760, 222);
            this.grid.TabIndex = 2;
            //
            // volumeLabel
            //
            this.volumeLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.volumeLabel.AutoSize = true;
            this.volumeLabel.Location = new System.Drawing.Point(11, 12);
            this.volumeLabel.Name = "volumeLabel";
            this.volumeLabel.Size = new System.Drawing.Size(64, 13);
            this.volumeLabel.TabIndex = 0;
            this.volumeLabel.Text = "Volume (dB)";
            //
            // volume
            //
            this.volume.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.volume.DecimalPlaces = 1;
            this.volume.Increment = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this.volume.Location = new System.Drawing.Point(96, 9);
            this.volume.Maximum = new decimal(new int[] {
            24,
            0,
            0,
            0});
            this.volume.Minimum = new decimal(new int[] {
            96,
            0,
            0,
            -2147483648});
            this.volume.Name = "volume";
            this.volume.Size = new System.Drawing.Size(110, 20);
            this.volume.TabIndex = 1;
            this.tips.SetToolTip(this.volume, "The volume of what the event plays, in dB - its own: the containers above it add theirs. Negative is quieter; 6 dB is about half or double.");
            this.volume.ValueChanged += new System.EventHandler(this.volume_ValueChanged);
            //
            // volumeOn
            //
            this.volumeOn.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.volumeOn.AutoSize = true;
            this.volumeOn.Location = new System.Drawing.Point(362, 10);
            this.volumeOn.Name = "volumeOn";
            this.volumeOn.Size = new System.Drawing.Size(63, 17);
            this.volumeOn.TabIndex = 2;
            this.volumeOn.Text = "Change";
            this.volumeOn.UseVisualStyleBackColor = true;
            this.volumeOn.CheckedChanged += new System.EventHandler(this.Change_CheckedChanged);
            //
            // volumeShip
            //
            this.volumeShip.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.volumeShip.AutoSize = true;
            this.volumeShip.ForeColor = System.Drawing.SystemColors.GrayText;
            this.volumeShip.Location = new System.Drawing.Point(431, 12);
            this.volumeShip.Name = "volumeShip";
            this.volumeShip.Size = new System.Drawing.Size(0, 13);
            this.volumeShip.TabIndex = 3;
            //
            // pitchLabel
            //
            this.pitchLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.pitchLabel.AutoSize = true;
            this.pitchLabel.Location = new System.Drawing.Point(11, 38);
            this.pitchLabel.Name = "pitchLabel";
            this.pitchLabel.Size = new System.Drawing.Size(66, 13);
            this.pitchLabel.TabIndex = 4;
            this.pitchLabel.Text = "Pitch (cents)";
            //
            // pitch
            //
            this.pitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.pitch.Increment = new decimal(new int[] {
            50,
            0,
            0,
            0});
            this.pitch.Location = new System.Drawing.Point(96, 35);
            this.pitch.Maximum = new decimal(new int[] {
            2400,
            0,
            0,
            0});
            this.pitch.Minimum = new decimal(new int[] {
            2400,
            0,
            0,
            -2147483648});
            this.pitch.Name = "pitch";
            this.pitch.Size = new System.Drawing.Size(110, 20);
            this.pitch.TabIndex = 5;
            this.tips.SetToolTip(this.pitch, "Pitch in cents: 100 is a semitone, 1200 an octave.");
            this.pitch.ValueChanged += new System.EventHandler(this.pitch_ValueChanged);
            //
            // pitchOn
            //
            this.pitchOn.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.pitchOn.AutoSize = true;
            this.pitchOn.Location = new System.Drawing.Point(362, 36);
            this.pitchOn.Name = "pitchOn";
            this.pitchOn.Size = new System.Drawing.Size(63, 17);
            this.pitchOn.TabIndex = 6;
            this.pitchOn.Text = "Change";
            this.pitchOn.UseVisualStyleBackColor = true;
            this.pitchOn.CheckedChanged += new System.EventHandler(this.Change_CheckedChanged);
            //
            // pitchShip
            //
            this.pitchShip.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.pitchShip.AutoSize = true;
            this.pitchShip.ForeColor = System.Drawing.SystemColors.GrayText;
            this.pitchShip.Location = new System.Drawing.Point(431, 38);
            this.pitchShip.Name = "pitchShip";
            this.pitchShip.Size = new System.Drawing.Size(0, 13);
            this.pitchShip.TabIndex = 7;
            //
            // delayLabel
            //
            this.delayLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.delayLabel.AutoSize = true;
            this.delayLabel.Location = new System.Drawing.Point(11, 64);
            this.delayLabel.Name = "delayLabel";
            this.delayLabel.Size = new System.Drawing.Size(79, 13);
            this.delayLabel.TabIndex = 8;
            this.delayLabel.Text = "Start delay (ms)";
            //
            // delay
            //
            this.delay.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.delay.Increment = new decimal(new int[] {
            100,
            0,
            0,
            0});
            this.delay.Location = new System.Drawing.Point(96, 61);
            this.delay.Maximum = new decimal(new int[] {
            600000,
            0,
            0,
            0});
            this.delay.Name = "delay";
            this.delay.Size = new System.Drawing.Size(110, 20);
            this.delay.TabIndex = 9;
            this.tips.SetToolTip(this.delay, "How long after the event is played its sound starts, in ms. A random delay range the game gave it is cleared.");
            this.delay.ValueChanged += new System.EventHandler(this.delay_ValueChanged);
            //
            // delayOn
            //
            this.delayOn.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.delayOn.AutoSize = true;
            this.delayOn.Location = new System.Drawing.Point(362, 62);
            this.delayOn.Name = "delayOn";
            this.delayOn.Size = new System.Drawing.Size(63, 17);
            this.delayOn.TabIndex = 10;
            this.delayOn.Text = "Change";
            this.delayOn.UseVisualStyleBackColor = true;
            this.delayOn.CheckedChanged += new System.EventHandler(this.Change_CheckedChanged);
            //
            // delayShip
            //
            this.delayShip.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.delayShip.AutoSize = true;
            this.delayShip.ForeColor = System.Drawing.SystemColors.GrayText;
            this.delayShip.Location = new System.Drawing.Point(431, 64);
            this.delayShip.Name = "delayShip";
            this.delayShip.Size = new System.Drawing.Size(0, 13);
            this.delayShip.TabIndex = 11;
            //
            // fadeInLabel
            //
            this.fadeInLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.fadeInLabel.AutoSize = true;
            this.fadeInLabel.Location = new System.Drawing.Point(11, 90);
            this.fadeInLabel.Name = "fadeInLabel";
            this.fadeInLabel.Size = new System.Drawing.Size(64, 13);
            this.fadeInLabel.TabIndex = 12;
            this.fadeInLabel.Text = "Fade in (ms)";
            //
            // fadeIn
            //
            this.fadeIn.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.fadeIn.Increment = new decimal(new int[] {
            100,
            0,
            0,
            0});
            this.fadeIn.Location = new System.Drawing.Point(96, 87);
            this.fadeIn.Maximum = new decimal(new int[] {
            600000,
            0,
            0,
            0});
            this.fadeIn.Name = "fadeIn";
            this.fadeIn.Size = new System.Drawing.Size(110, 20);
            this.fadeIn.TabIndex = 13;
            this.tips.SetToolTip(this.fadeIn, "How long the sound takes to fade in as it starts, in ms.");
            this.fadeIn.ValueChanged += new System.EventHandler(this.fadeIn_ValueChanged);
            //
            // fadeInOn
            //
            this.fadeInOn.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.fadeInOn.AutoSize = true;
            this.fadeInOn.Location = new System.Drawing.Point(362, 88);
            this.fadeInOn.Name = "fadeInOn";
            this.fadeInOn.Size = new System.Drawing.Size(63, 17);
            this.fadeInOn.TabIndex = 14;
            this.fadeInOn.Text = "Change";
            this.fadeInOn.UseVisualStyleBackColor = true;
            this.fadeInOn.CheckedChanged += new System.EventHandler(this.Change_CheckedChanged);
            //
            // fadeInShip
            //
            this.fadeInShip.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.fadeInShip.AutoSize = true;
            this.fadeInShip.ForeColor = System.Drawing.SystemColors.GrayText;
            this.fadeInShip.Location = new System.Drawing.Point(431, 90);
            this.fadeInShip.Name = "fadeInShip";
            this.fadeInShip.Size = new System.Drawing.Size(0, 13);
            this.fadeInShip.TabIndex = 15;
            //
            // fadeOutLabel
            //
            this.fadeOutLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.fadeOutLabel.AutoSize = true;
            this.fadeOutLabel.Location = new System.Drawing.Point(11, 116);
            this.fadeOutLabel.Name = "fadeOutLabel";
            this.fadeOutLabel.Size = new System.Drawing.Size(71, 13);
            this.fadeOutLabel.TabIndex = 16;
            this.fadeOutLabel.Text = "Fade out (ms)";
            //
            // fadeOut
            //
            this.fadeOut.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.fadeOut.Increment = new decimal(new int[] {
            100,
            0,
            0,
            0});
            this.fadeOut.Location = new System.Drawing.Point(96, 113);
            this.fadeOut.Maximum = new decimal(new int[] {
            600000,
            0,
            0,
            0});
            this.fadeOut.Name = "fadeOut";
            this.fadeOut.Size = new System.Drawing.Size(110, 20);
            this.fadeOut.TabIndex = 17;
            this.tips.SetToolTip(this.fadeOut, "How long the sound takes to fade out when its stop event stops it, in ms (a Play event's goes on its Stop_ event). A Sound entity fades only when it has that stop_event: Sound.stop on its own is instant.");
            this.fadeOut.ValueChanged += new System.EventHandler(this.fadeOut_ValueChanged);
            //
            // fadeOutOn
            //
            this.fadeOutOn.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.fadeOutOn.AutoSize = true;
            this.fadeOutOn.Location = new System.Drawing.Point(362, 114);
            this.fadeOutOn.Name = "fadeOutOn";
            this.fadeOutOn.Size = new System.Drawing.Size(63, 17);
            this.fadeOutOn.TabIndex = 18;
            this.fadeOutOn.Text = "Change";
            this.fadeOutOn.UseVisualStyleBackColor = true;
            this.fadeOutOn.CheckedChanged += new System.EventHandler(this.Change_CheckedChanged);
            //
            // fadeOutShip
            //
            this.fadeOutShip.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.fadeOutShip.AutoSize = true;
            this.fadeOutShip.ForeColor = System.Drawing.SystemColors.GrayText;
            this.fadeOutShip.Location = new System.Drawing.Point(431, 116);
            this.fadeOutShip.Name = "fadeOutShip";
            this.fadeOutShip.Size = new System.Drawing.Size(0, 13);
            this.fadeOutShip.TabIndex = 19;
            //
            // stopsLabel
            //
            this.stopsLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.stopsLabel.AutoSize = true;
            this.stopsLabel.Location = new System.Drawing.Point(11, 143);
            this.stopsLabel.Name = "stopsLabel";
            this.stopsLabel.Size = new System.Drawing.Size(70, 13);
            this.stopsLabel.TabIndex = 20;
            this.stopsLabel.Text = "Stop reaches";
            //
            // stops
            //
            this.stops.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.stops.Items.AddRange(new object[] {
            "Its own sound",
            "Everything under its actor-mixer",
            "Everything on its bus"});
            this.stops.Location = new System.Drawing.Point(96, 139);
            this.stops.Name = "stops";
            this.stops.Size = new System.Drawing.Size(260, 21);
            this.stops.TabIndex = 21;
            this.tips.SetToolTip(this.stops, "What the stop event stops: the sound it was made for, everything under the same actor-mixer (a music system's other tracks, say), or everything playing on the same bus.");
            //
            // stopsOn
            //
            this.stopsOn.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.stopsOn.AutoSize = true;
            this.stopsOn.Location = new System.Drawing.Point(362, 141);
            this.stopsOn.Name = "stopsOn";
            this.stopsOn.Size = new System.Drawing.Size(63, 17);
            this.stopsOn.TabIndex = 22;
            this.stopsOn.Text = "Change";
            this.stopsOn.UseVisualStyleBackColor = true;
            this.stopsOn.CheckedChanged += new System.EventHandler(this.Change_CheckedChanged);
            //
            // stopsShip
            //
            this.stopsShip.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.stopsShip.AutoSize = true;
            this.stopsShip.ForeColor = System.Drawing.SystemColors.GrayText;
            this.stopsShip.Location = new System.Drawing.Point(431, 143);
            this.stopsShip.Name = "stopsShip";
            this.stopsShip.Size = new System.Drawing.Size(0, 13);
            this.stopsShip.TabIndex = 23;
            //
            // loopLabel
            //
            this.loopLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.loopLabel.AutoSize = true;
            this.loopLabel.Location = new System.Drawing.Point(11, 170);
            this.loopLabel.Name = "loopLabel";
            this.loopLabel.Size = new System.Drawing.Size(31, 13);
            this.loopLabel.TabIndex = 24;
            this.loopLabel.Text = "Loop";
            //
            // loopRow
            //
            this.loopRow.AutoSize = true;
            this.loopRow.Controls.Add(this.loop);
            this.loopRow.Controls.Add(this.loopCount);
            this.loopRow.Location = new System.Drawing.Point(93, 163);
            this.loopRow.Margin = new System.Windows.Forms.Padding(0);
            this.loopRow.Name = "loopRow";
            this.loopRow.Size = new System.Drawing.Size(242, 27);
            this.loopRow.TabIndex = 25;
            this.loopRow.WrapContents = false;
            //
            // loop
            //
            this.loop.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.loop.Items.AddRange(new object[] {
            "Once",
            "For ever",
            "Times:"});
            this.loop.Location = new System.Drawing.Point(3, 3);
            this.loop.Name = "loop";
            this.loop.Size = new System.Drawing.Size(120, 21);
            this.loop.TabIndex = 0;
            this.tips.SetToolTip(this.loop, "Whether every sound the event plays repeats: once (no loop), for ever (until stopped), or a number of times.");
            //
            // loopCount
            //
            this.loopCount.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.loopCount.Enabled = false;
            this.loopCount.Location = new System.Drawing.Point(129, 3);
            this.loopCount.Maximum = new decimal(new int[] {
            32767,
            0,
            0,
            0});
            this.loopCount.Minimum = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.loopCount.Name = "loopCount";
            this.loopCount.Size = new System.Drawing.Size(110, 20);
            this.loopCount.TabIndex = 1;
            this.loopCount.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.loopCount.ValueChanged += new System.EventHandler(this.loopCount_ValueChanged);
            //
            // loopOn
            //
            this.loopOn.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.loopOn.AutoSize = true;
            this.loopOn.Location = new System.Drawing.Point(362, 168);
            this.loopOn.Name = "loopOn";
            this.loopOn.Size = new System.Drawing.Size(63, 17);
            this.loopOn.TabIndex = 26;
            this.loopOn.Text = "Change";
            this.loopOn.UseVisualStyleBackColor = true;
            this.loopOn.CheckedChanged += new System.EventHandler(this.Change_CheckedChanged);
            //
            // loopShip
            //
            this.loopShip.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.loopShip.AutoSize = true;
            this.loopShip.ForeColor = System.Drawing.SystemColors.GrayText;
            this.loopShip.Location = new System.Drawing.Point(431, 170);
            this.loopShip.Name = "loopShip";
            this.loopShip.Size = new System.Drawing.Size(0, 13);
            this.loopShip.TabIndex = 27;
            //
            // reachLabel
            //
            this.reachLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.reachLabel.AutoSize = true;
            this.reachLabel.Location = new System.Drawing.Point(11, 196);
            this.reachLabel.Name = "reachLabel";
            this.reachLabel.Size = new System.Drawing.Size(56, 13);
            this.reachLabel.TabIndex = 28;
            this.reachLabel.Text = "Reach (m)";
            //
            // reach
            //
            this.reach.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.reach.DecimalPlaces = 1;
            this.reach.Location = new System.Drawing.Point(96, 193);
            this.reach.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.reach.Minimum = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this.reach.Name = "reach";
            this.reach.Size = new System.Drawing.Size(110, 20);
            this.reach.TabIndex = 29;
            this.tips.SetToolTip(this.reach, "How far away the event can be heard, in metres. Its distance curves are copied for this event alone and stretched to end here - the curve other sounds share is left as it is.");
            this.reach.Value = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this.reach.ValueChanged += new System.EventHandler(this.reach_ValueChanged);
            //
            // reachOn
            //
            this.reachOn.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.reachOn.AutoSize = true;
            this.reachOn.Location = new System.Drawing.Point(362, 194);
            this.reachOn.Name = "reachOn";
            this.reachOn.Size = new System.Drawing.Size(63, 17);
            this.reachOn.TabIndex = 30;
            this.reachOn.Text = "Change";
            this.reachOn.UseVisualStyleBackColor = true;
            this.reachOn.CheckedChanged += new System.EventHandler(this.Change_CheckedChanged);
            //
            // reachShip
            //
            this.reachShip.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.reachShip.AutoSize = true;
            this.reachShip.ForeColor = System.Drawing.SystemColors.GrayText;
            this.reachShip.Location = new System.Drawing.Point(431, 196);
            this.reachShip.Name = "reachShip";
            this.reachShip.Size = new System.Drawing.Size(0, 13);
            this.reachShip.TabIndex = 31;
            //
            // eventRow
            //
            this.eventRow.ColumnCount = 3;
            this.eventRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.eventRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.eventRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.eventRow.Controls.Add(this.eventLabel, 0, 0);
            this.eventRow.Controls.Add(this.eventName, 1, 0);
            this.eventRow.Controls.Add(this.readBtn, 2, 0);
            this.eventRow.Dock = System.Windows.Forms.DockStyle.Top;
            this.eventRow.Location = new System.Drawing.Point(0, 0);
            this.eventRow.Name = "eventRow";
            this.eventRow.Padding = new System.Windows.Forms.Padding(6, 6, 6, 0);
            this.eventRow.RowCount = 1;
            this.eventRow.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.eventRow.Size = new System.Drawing.Size(760, 32);
            this.eventRow.TabIndex = 3;
            //
            // eventLabel
            //
            this.eventLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.eventLabel.AutoSize = true;
            this.eventLabel.Location = new System.Drawing.Point(9, 14);
            this.eventLabel.Name = "eventLabel";
            this.eventLabel.Size = new System.Drawing.Size(71, 13);
            this.eventLabel.TabIndex = 0;
            this.eventLabel.Text = "Sound event:";
            //
            // eventName
            //
            this.eventName.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.eventName.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.CustomSource;
            this.eventName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.eventName.Location = new System.Drawing.Point(86, 9);
            this.eventName.Name = "eventName";
            this.eventName.Size = new System.Drawing.Size(589, 20);
            this.eventName.TabIndex = 1;
            this.tips.SetToolTip(this.eventName, "The sound event to tune, as a Sound entity's sound_event names it.");
            this.eventName.KeyDown += new System.Windows.Forms.KeyEventHandler(this.eventName_KeyDown);
            //
            // readBtn
            //
            this.readBtn.Location = new System.Drawing.Point(681, 9);
            this.readBtn.Name = "readBtn";
            this.readBtn.Size = new System.Drawing.Size(70, 24);
            this.readBtn.TabIndex = 2;
            this.readBtn.Text = "Read";
            this.readBtn.UseVisualStyleBackColor = true;
            this.readBtn.Click += new System.EventHandler(this.readBtn_Click);
            //
            // LevelSoundTuneDialog
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.closeBtn;
            this.ClientSize = new System.Drawing.Size(760, 600);
            this.Controls.Add(this.info);
            this.Controls.Add(this.buttons);
            this.Controls.Add(this.grid);
            this.Controls.Add(this.eventRow);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(640, 540);
            this.Name = "LevelSoundTuneDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Tune Sound Event";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.LevelSoundTuneDialog_FormClosed);
            this.info.ResumeLayout(false);
            this.info.PerformLayout();
            this.buttons.ResumeLayout(false);
            this.grid.ResumeLayout(false);
            this.grid.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.volume)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pitch)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.delay)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.fadeIn)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.fadeOut)).EndInit();
            this.loopRow.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.loopCount)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.reach)).EndInit();
            this.eventRow.ResumeLayout(false);
            this.eventRow.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox info;
        private System.Windows.Forms.TextBox about;
        private System.Windows.Forms.FlowLayoutPanel buttons;
        private System.Windows.Forms.Button closeBtn;
        private System.Windows.Forms.Button revertBtn;
        private System.Windows.Forms.Button applyBtn;
        private System.Windows.Forms.TableLayoutPanel grid;
        private System.Windows.Forms.Label volumeLabel;
        private System.Windows.Forms.NumericUpDown volume;
        private System.Windows.Forms.CheckBox volumeOn;
        private System.Windows.Forms.Label volumeShip;
        private System.Windows.Forms.Label pitchLabel;
        private System.Windows.Forms.NumericUpDown pitch;
        private System.Windows.Forms.CheckBox pitchOn;
        private System.Windows.Forms.Label pitchShip;
        private System.Windows.Forms.Label delayLabel;
        private System.Windows.Forms.NumericUpDown delay;
        private System.Windows.Forms.CheckBox delayOn;
        private System.Windows.Forms.Label delayShip;
        private System.Windows.Forms.Label fadeInLabel;
        private System.Windows.Forms.NumericUpDown fadeIn;
        private System.Windows.Forms.CheckBox fadeInOn;
        private System.Windows.Forms.Label fadeInShip;
        private System.Windows.Forms.Label fadeOutLabel;
        private System.Windows.Forms.NumericUpDown fadeOut;
        private System.Windows.Forms.CheckBox fadeOutOn;
        private System.Windows.Forms.Label fadeOutShip;
        private System.Windows.Forms.Label stopsLabel;
        private System.Windows.Forms.ComboBox stops;
        private System.Windows.Forms.CheckBox stopsOn;
        private System.Windows.Forms.Label stopsShip;
        private System.Windows.Forms.Label loopLabel;
        private System.Windows.Forms.FlowLayoutPanel loopRow;
        private System.Windows.Forms.ComboBox loop;
        private System.Windows.Forms.NumericUpDown loopCount;
        private System.Windows.Forms.CheckBox loopOn;
        private System.Windows.Forms.Label loopShip;
        private System.Windows.Forms.Label reachLabel;
        private System.Windows.Forms.NumericUpDown reach;
        private System.Windows.Forms.CheckBox reachOn;
        private System.Windows.Forms.Label reachShip;
        private System.Windows.Forms.TableLayoutPanel eventRow;
        private System.Windows.Forms.Label eventLabel;
        private System.Windows.Forms.TextBox eventName;
        private System.Windows.Forms.Button readBtn;
        private System.Windows.Forms.ToolTip tips;
    }
}

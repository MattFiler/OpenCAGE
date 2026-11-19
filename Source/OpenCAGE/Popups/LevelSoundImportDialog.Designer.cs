namespace OpenCAGE
{
    partial class LevelSoundImportDialog
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
            this.root = new System.Windows.Forms.TableLayoutPanel();
            this.wavRow = new System.Windows.Forms.TableLayoutPanel();
            this.wavLabel = new System.Windows.Forms.Label();
            this.wavPath = new System.Windows.Forms.TextBox();
            this.browseBtn = new System.Windows.Forms.Button();
            this.replaceRadio = new System.Windows.Forms.RadioButton();
            this.searchRow = new System.Windows.Forms.TableLayoutPanel();
            this.searchLabel = new System.Windows.Forms.Label();
            this.search = new System.Windows.Forms.TextBox();
            this.onlyBank = new System.Windows.Forms.CheckBox();
            this.list = new System.Windows.Forms.ListView();
            this.soundColumn = new System.Windows.Forms.ColumnHeader();
            this.idColumn = new System.Windows.Forms.ColumnHeader();
            this.keptInColumn = new System.Windows.Forms.ColumnHeader();
            this.othersColumn = new System.Windows.Forms.ColumnHeader();
            this.streamRow = new System.Windows.Forms.FlowLayoutPanel();
            this.streamRadio = new System.Windows.Forms.RadioButton();
            this.streamId = new System.Windows.Forms.TextBox();
            this.optionsRow = new System.Windows.Forms.FlowLayoutPanel();
            this.channelsLabel = new System.Windows.Forms.Label();
            this.channels = new System.Windows.Forms.ComboBox();
            this.rateLabel = new System.Windows.Forms.Label();
            this.rate = new System.Windows.Forms.ComboBox();
            this.qualityLabel = new System.Windows.Forms.Label();
            this.quality = new System.Windows.Forms.NumericUpDown();
            this.gainLabel = new System.Windows.Forms.Label();
            this.gain = new System.Windows.Forms.NumericUpDown();
            this.normalise = new System.Windows.Forms.CheckBox();
            this.summary = new System.Windows.Forms.TextBox();
            this.buttonRow = new System.Windows.Forms.FlowLayoutPanel();
            this.cancelBtn = new System.Windows.Forms.Button();
            this.importBtn = new System.Windows.Forms.Button();
            this.stopBtn = new System.Windows.Forms.Button();
            this.playNewBtn = new System.Windows.Forms.Button();
            this.playOriginalBtn = new System.Windows.Forms.Button();
            this.tips = new System.Windows.Forms.ToolTip(this.components);
            this.debounce = new System.Windows.Forms.Timer(this.components);
            this.root.SuspendLayout();
            this.wavRow.SuspendLayout();
            this.searchRow.SuspendLayout();
            this.streamRow.SuspendLayout();
            this.optionsRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.quality)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gain)).BeginInit();
            this.buttonRow.SuspendLayout();
            this.SuspendLayout();
            //
            // root
            //
            this.root.ColumnCount = 1;
            this.root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.root.Controls.Add(this.wavRow, 0, 0);
            this.root.Controls.Add(this.replaceRadio, 0, 1);
            this.root.Controls.Add(this.searchRow, 0, 2);
            this.root.Controls.Add(this.list, 0, 3);
            this.root.Controls.Add(this.streamRow, 0, 4);
            this.root.Controls.Add(this.optionsRow, 0, 5);
            this.root.Controls.Add(this.summary, 0, 6);
            this.root.Controls.Add(this.buttonRow, 0, 7);
            this.root.Dock = System.Windows.Forms.DockStyle.Fill;
            this.root.Location = new System.Drawing.Point(0, 0);
            this.root.Name = "root";
            this.root.Padding = new System.Windows.Forms.Padding(8);
            this.root.RowCount = 8;
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.root.Size = new System.Drawing.Size(860, 660);
            this.root.TabIndex = 0;
            //
            // wavRow
            //
            this.wavRow.AutoSize = true;
            this.wavRow.ColumnCount = 3;
            this.wavRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.wavRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.wavRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.wavRow.Controls.Add(this.wavLabel, 0, 0);
            this.wavRow.Controls.Add(this.wavPath, 1, 0);
            this.wavRow.Controls.Add(this.browseBtn, 2, 0);
            this.wavRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.wavRow.Location = new System.Drawing.Point(8, 8);
            this.wavRow.Margin = new System.Windows.Forms.Padding(0);
            this.wavRow.Name = "wavRow";
            this.wavRow.RowCount = 1;
            this.wavRow.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.wavRow.Size = new System.Drawing.Size(844, 30);
            this.wavRow.TabIndex = 0;
            //
            // wavLabel
            //
            this.wavLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.wavLabel.AutoSize = true;
            this.wavLabel.Location = new System.Drawing.Point(3, 10);
            this.wavLabel.Margin = new System.Windows.Forms.Padding(3, 7, 3, 3);
            this.wavLabel.Name = "wavLabel";
            this.wavLabel.Size = new System.Drawing.Size(85, 13);
            this.wavLabel.TabIndex = 0;
            this.wavLabel.Text = "Audio file (.wav):";
            //
            // wavPath
            //
            this.wavPath.Dock = System.Windows.Forms.DockStyle.Fill;
            this.wavPath.Location = new System.Drawing.Point(94, 3);
            this.wavPath.Name = "wavPath";
            this.wavPath.ReadOnly = true;
            this.wavPath.Size = new System.Drawing.Size(651, 20);
            this.wavPath.TabIndex = 1;
            //
            // browseBtn
            //
            this.browseBtn.Location = new System.Drawing.Point(751, 3);
            this.browseBtn.Name = "browseBtn";
            this.browseBtn.Size = new System.Drawing.Size(90, 24);
            this.browseBtn.TabIndex = 2;
            this.browseBtn.Text = "Browse...";
            this.tips.SetToolTip(this.browseBtn, "The audio to import: an uncompressed .wav (8 to 32 bit, or floating point), any sample rate and channel count. A .wav can also be dropped on this window.");
            this.browseBtn.UseVisualStyleBackColor = true;
            this.browseBtn.Click += new System.EventHandler(this.browseBtn_Click);
            //
            // replaceRadio
            //
            this.replaceRadio.AutoSize = true;
            this.replaceRadio.Checked = true;
            this.replaceRadio.Location = new System.Drawing.Point(11, 46);
            this.replaceRadio.Margin = new System.Windows.Forms.Padding(3, 8, 3, 3);
            this.replaceRadio.Name = "replaceRadio";
            this.replaceRadio.Size = new System.Drawing.Size(234, 17);
            this.replaceRadio.TabIndex = 1;
            this.replaceRadio.TabStop = true;
            this.replaceRadio.Text = "Replace a sound of the game's on this level:";
            this.tips.SetToolTip(this.replaceRadio, "Put the new audio in place of one of the game's sounds, for this level only: streamed audio goes in as a stream of its own, audio held in a soundbank goes into the package's copy of that soundbank.");
            this.replaceRadio.UseVisualStyleBackColor = true;
            this.replaceRadio.CheckedChanged += new System.EventHandler(this.replaceRadio_CheckedChanged);
            //
            // searchRow
            //
            this.searchRow.AutoSize = true;
            this.searchRow.ColumnCount = 3;
            this.searchRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.searchRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.searchRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.searchRow.Controls.Add(this.searchLabel, 0, 0);
            this.searchRow.Controls.Add(this.search, 1, 0);
            this.searchRow.Controls.Add(this.onlyBank, 2, 0);
            this.searchRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.searchRow.Location = new System.Drawing.Point(24, 66);
            this.searchRow.Margin = new System.Windows.Forms.Padding(16, 0, 0, 0);
            this.searchRow.Name = "searchRow";
            this.searchRow.RowCount = 1;
            this.searchRow.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.searchRow.Size = new System.Drawing.Size(828, 26);
            this.searchRow.TabIndex = 2;
            //
            // searchLabel
            //
            this.searchLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.searchLabel.AutoSize = true;
            this.searchLabel.Location = new System.Drawing.Point(3, 8);
            this.searchLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.searchLabel.Name = "searchLabel";
            this.searchLabel.Size = new System.Drawing.Size(135, 13);
            this.searchLabel.TabIndex = 0;
            this.searchLabel.Text = "Search (sound event or id):";
            //
            // search
            //
            this.search.Dock = System.Windows.Forms.DockStyle.Fill;
            this.search.Location = new System.Drawing.Point(144, 3);
            this.search.Name = "search";
            this.search.Size = new System.Drawing.Size(681, 20);
            this.search.TabIndex = 1;
            this.search.TextChanged += new System.EventHandler(this.search_TextChanged);
            //
            // onlyBank
            //
            this.onlyBank.AutoSize = true;
            this.onlyBank.Location = new System.Drawing.Point(0, 0);
            this.onlyBank.Name = "onlyBank";
            this.onlyBank.Size = new System.Drawing.Size(104, 24);
            this.onlyBank.TabIndex = 2;
            this.onlyBank.UseVisualStyleBackColor = true;
            this.onlyBank.Visible = false;
            //
            // list
            //
            this.list.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.soundColumn,
            this.idColumn,
            this.keptInColumn,
            this.othersColumn});
            this.list.Dock = System.Windows.Forms.DockStyle.Fill;
            this.list.FullRowSelect = true;
            this.list.HideSelection = false;
            this.list.Location = new System.Drawing.Point(27, 95);
            this.list.Margin = new System.Windows.Forms.Padding(19, 3, 3, 3);
            this.list.MultiSelect = false;
            this.list.Name = "list";
            this.list.Size = new System.Drawing.Size(822, 279);
            this.list.TabIndex = 3;
            this.list.View = System.Windows.Forms.View.Details;
            this.list.SelectedIndexChanged += new System.EventHandler(this.list_SelectedIndexChanged);
            //
            // soundColumn
            //
            this.soundColumn.Text = "Sound";
            this.soundColumn.Width = 310;
            //
            // idColumn
            //
            this.idColumn.Text = "Id";
            this.idColumn.Width = 85;
            //
            // keptInColumn
            //
            this.keptInColumn.Text = "Kept in";
            this.keptInColumn.Width = 220;
            //
            // othersColumn
            //
            this.othersColumn.Text = "Also played by";
            this.othersColumn.Width = 165;
            //
            // streamRow
            //
            this.streamRow.AutoSize = true;
            this.streamRow.Controls.Add(this.streamRadio);
            this.streamRow.Controls.Add(this.streamId);
            this.streamRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.streamRow.Location = new System.Drawing.Point(8, 381);
            this.streamRow.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.streamRow.Name = "streamRow";
            this.streamRow.Size = new System.Drawing.Size(844, 26);
            this.streamRow.TabIndex = 4;
            this.streamRow.WrapContents = false;
            //
            // streamRadio
            //
            this.streamRadio.AutoSize = true;
            this.streamRadio.Location = new System.Drawing.Point(3, 5);
            this.streamRadio.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.streamRadio.Name = "streamRadio";
            this.streamRadio.Size = new System.Drawing.Size(214, 17);
            this.streamRadio.TabIndex = 0;
            this.streamRadio.Text = "Add it as a stream of its own, with the id:";
            this.tips.SetToolTip(this.streamRadio, "Put the new audio in the package as a stream with an id of your choosing - for a soundbank of your own (added with Add Files) that streams audio by that id.");
            this.streamRadio.UseVisualStyleBackColor = true;
            this.streamRadio.CheckedChanged += new System.EventHandler(this.streamRadio_CheckedChanged);
            //
            // streamId
            //
            this.streamId.Location = new System.Drawing.Point(223, 3);
            this.streamId.Name = "streamId";
            this.streamId.Size = new System.Drawing.Size(120, 20);
            this.streamId.TabIndex = 1;
            this.streamId.TextChanged += new System.EventHandler(this.streamId_TextChanged);
            this.streamId.Enter += new System.EventHandler(this.streamId_Enter);
            //
            // optionsRow
            //
            this.optionsRow.AutoSize = true;
            this.optionsRow.Controls.Add(this.channelsLabel);
            this.optionsRow.Controls.Add(this.channels);
            this.optionsRow.Controls.Add(this.rateLabel);
            this.optionsRow.Controls.Add(this.rate);
            this.optionsRow.Controls.Add(this.qualityLabel);
            this.optionsRow.Controls.Add(this.quality);
            this.optionsRow.Controls.Add(this.gainLabel);
            this.optionsRow.Controls.Add(this.gain);
            this.optionsRow.Controls.Add(this.normalise);
            this.optionsRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.optionsRow.Location = new System.Drawing.Point(8, 413);
            this.optionsRow.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this.optionsRow.Name = "optionsRow";
            this.optionsRow.Size = new System.Drawing.Size(844, 53);
            this.optionsRow.TabIndex = 5;
            //
            // channelsLabel
            //
            this.channelsLabel.AutoSize = true;
            this.channelsLabel.Location = new System.Drawing.Point(8, 7);
            this.channelsLabel.Margin = new System.Windows.Forms.Padding(8, 7, 0, 3);
            this.channelsLabel.Name = "channelsLabel";
            this.channelsLabel.Size = new System.Drawing.Size(54, 13);
            this.channelsLabel.TabIndex = 0;
            this.channelsLabel.Text = "Channels:";
            //
            // channels
            //
            this.channels.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.channels.Location = new System.Drawing.Point(65, 3);
            this.channels.Name = "channels";
            this.channels.Size = new System.Drawing.Size(230, 21);
            this.channels.TabIndex = 1;
            this.tips.SetToolTip(this.channels, "Mono or stereo. Matching the sound it replaces keeps it positioned and mixed as the game's designers had it; the game's codebooks only cover mono and stereo, so wider audio is always mixed down.");
            //
            // rateLabel
            //
            this.rateLabel.AutoSize = true;
            this.rateLabel.Location = new System.Drawing.Point(306, 7);
            this.rateLabel.Margin = new System.Windows.Forms.Padding(8, 7, 0, 3);
            this.rateLabel.Name = "rateLabel";
            this.rateLabel.Size = new System.Drawing.Size(66, 13);
            this.rateLabel.TabIndex = 2;
            this.rateLabel.Text = "Sample rate:";
            //
            // rate
            //
            this.rate.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.rate.Location = new System.Drawing.Point(375, 3);
            this.rate.Name = "rate";
            this.rate.Size = new System.Drawing.Size(230, 21);
            this.rate.TabIndex = 3;
            this.tips.SetToolTip(this.rate, "The sample rate to encode at. A rate the game's decoder isn't set up for (below 16000 Hz mono or 32000 Hz stereo, or above 48000 Hz) moves to the nearest one it is.");
            //
            // qualityLabel
            //
            this.qualityLabel.AutoSize = true;
            this.qualityLabel.Location = new System.Drawing.Point(616, 7);
            this.qualityLabel.Margin = new System.Windows.Forms.Padding(8, 7, 0, 3);
            this.qualityLabel.Name = "qualityLabel";
            this.qualityLabel.Size = new System.Drawing.Size(42, 13);
            this.qualityLabel.TabIndex = 4;
            this.qualityLabel.Text = "Quality:";
            //
            // quality
            //
            this.quality.DecimalPlaces = 1;
            this.quality.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.quality.Location = new System.Drawing.Point(661, 3);
            this.quality.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.quality.Minimum = new decimal(new int[] {
            3,
            0,
            0,
            65536});
            this.quality.Name = "quality";
            this.quality.Size = new System.Drawing.Size(52, 20);
            this.quality.TabIndex = 5;
            this.tips.SetToolTip(this.quality, "Vorbis quality, 0.3 to 1.0. Raised automatically to the nearest setting the game's decoder is set up for (0.5 or 0.6 for most audio).");
            this.quality.Value = new decimal(new int[] {
            6,
            0,
            0,
            65536});
            this.quality.ValueChanged += new System.EventHandler(this.Option_Changed);
            //
            // gainLabel
            //
            this.gainLabel.AutoSize = true;
            this.gainLabel.Location = new System.Drawing.Point(724, 7);
            this.gainLabel.Margin = new System.Windows.Forms.Padding(8, 7, 0, 3);
            this.gainLabel.Name = "gainLabel";
            this.gainLabel.Size = new System.Drawing.Size(54, 13);
            this.gainLabel.TabIndex = 6;
            this.gainLabel.Text = "Gain (dB):";
            //
            // gain
            //
            this.gain.DecimalPlaces = 1;
            this.gain.Location = new System.Drawing.Point(781, 3);
            this.gain.Maximum = new decimal(new int[] {
            40,
            0,
            0,
            0});
            this.gain.Minimum = new decimal(new int[] {
            40,
            0,
            0,
            -2147483648});
            this.gain.Name = "gain";
            this.gain.Size = new System.Drawing.Size(58, 20);
            this.gain.TabIndex = 7;
            this.tips.SetToolTip(this.gain, "Decibels added to the audio; negative makes it quieter.");
            this.gain.ValueChanged += new System.EventHandler(this.Option_Changed);
            //
            // normalise
            //
            this.normalise.AutoSize = true;
            this.normalise.Location = new System.Drawing.Point(8, 33);
            this.normalise.Margin = new System.Windows.Forms.Padding(8, 6, 3, 3);
            this.normalise.Name = "normalise";
            this.normalise.Size = new System.Drawing.Size(125, 17);
            this.normalise.TabIndex = 8;
            this.normalise.Text = "Normalise to -1 dBFS";
            this.tips.SetToolTip(this.normalise, "Scale the audio so its loudest moment peaks at -1 dBFS (any gain is folded into it).");
            this.normalise.UseVisualStyleBackColor = true;
            this.normalise.CheckedChanged += new System.EventHandler(this.Option_Changed);
            //
            // summary
            //
            this.summary.Dock = System.Windows.Forms.DockStyle.Fill;
            this.summary.Location = new System.Drawing.Point(11, 472);
            this.summary.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.summary.Multiline = true;
            this.summary.Name = "summary";
            this.summary.ReadOnly = true;
            this.summary.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.summary.Size = new System.Drawing.Size(838, 141);
            this.summary.TabIndex = 6;
            //
            // buttonRow
            //
            this.buttonRow.AutoSize = true;
            this.buttonRow.Controls.Add(this.cancelBtn);
            this.buttonRow.Controls.Add(this.importBtn);
            this.buttonRow.Controls.Add(this.stopBtn);
            this.buttonRow.Controls.Add(this.playNewBtn);
            this.buttonRow.Controls.Add(this.playOriginalBtn);
            this.buttonRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonRow.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.buttonRow.Location = new System.Drawing.Point(8, 620);
            this.buttonRow.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.buttonRow.Name = "buttonRow";
            this.buttonRow.Size = new System.Drawing.Size(844, 32);
            this.buttonRow.TabIndex = 7;
            this.buttonRow.WrapContents = false;
            //
            // cancelBtn
            //
            this.cancelBtn.Location = new System.Drawing.Point(751, 3);
            this.cancelBtn.Name = "cancelBtn";
            this.cancelBtn.Size = new System.Drawing.Size(90, 26);
            this.cancelBtn.TabIndex = 0;
            this.cancelBtn.Text = "Cancel";
            this.cancelBtn.UseVisualStyleBackColor = true;
            this.cancelBtn.Click += new System.EventHandler(this.cancelBtn_Click);
            //
            // importBtn
            //
            this.importBtn.Enabled = false;
            this.importBtn.Location = new System.Drawing.Point(655, 3);
            this.importBtn.Name = "importBtn";
            this.importBtn.Size = new System.Drawing.Size(90, 26);
            this.importBtn.TabIndex = 1;
            this.importBtn.Text = "Import";
            this.importBtn.UseVisualStyleBackColor = true;
            this.importBtn.Click += new System.EventHandler(this.importBtn_Click);
            //
            // stopBtn
            //
            this.stopBtn.Enabled = false;
            this.stopBtn.Location = new System.Drawing.Point(589, 3);
            this.stopBtn.Name = "stopBtn";
            this.stopBtn.Size = new System.Drawing.Size(60, 26);
            this.stopBtn.TabIndex = 2;
            this.stopBtn.Text = "Stop";
            this.stopBtn.UseVisualStyleBackColor = true;
            this.stopBtn.Click += new System.EventHandler(this.stopBtn_Click);
            //
            // playNewBtn
            //
            this.playNewBtn.Enabled = false;
            this.playNewBtn.Location = new System.Drawing.Point(493, 3);
            this.playNewBtn.Name = "playNewBtn";
            this.playNewBtn.Size = new System.Drawing.Size(90, 26);
            this.playNewBtn.TabIndex = 3;
            this.playNewBtn.Text = "Play New";
            this.tips.SetToolTip(this.playNewBtn, "Play the new audio, decoded from the very .wem that will go in the level's package.");
            this.playNewBtn.UseVisualStyleBackColor = true;
            this.playNewBtn.Click += new System.EventHandler(this.playNewBtn_Click);
            //
            // playOriginalBtn
            //
            this.playOriginalBtn.Enabled = false;
            this.playOriginalBtn.Location = new System.Drawing.Point(387, 3);
            this.playOriginalBtn.Name = "playOriginalBtn";
            this.playOriginalBtn.Size = new System.Drawing.Size(100, 26);
            this.playOriginalBtn.TabIndex = 4;
            this.playOriginalBtn.Text = "Play Original";
            this.tips.SetToolTip(this.playOriginalBtn, "Play the game's audio for the selected sound.");
            this.playOriginalBtn.UseVisualStyleBackColor = true;
            this.playOriginalBtn.Click += new System.EventHandler(this.playOriginalBtn_Click);
            //
            // debounce
            //
            this.debounce.Interval = 350;
            this.debounce.Tick += new System.EventHandler(this.debounce_Tick);
            //
            // LevelSoundImportDialog
            //
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelBtn;
            this.ClientSize = new System.Drawing.Size(860, 660);
            this.Controls.Add(this.root);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(640, 520);
            this.Name = "LevelSoundImportDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Import WAV";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.LevelSoundImportDialog_FormClosed);
            this.Load += new System.EventHandler(this.LevelSoundImportDialog_Load);
            this.root.ResumeLayout(false);
            this.root.PerformLayout();
            this.wavRow.ResumeLayout(false);
            this.wavRow.PerformLayout();
            this.searchRow.ResumeLayout(false);
            this.searchRow.PerformLayout();
            this.streamRow.ResumeLayout(false);
            this.streamRow.PerformLayout();
            this.optionsRow.ResumeLayout(false);
            this.optionsRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.quality)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gain)).EndInit();
            this.buttonRow.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel root;
        private System.Windows.Forms.TableLayoutPanel wavRow;
        private System.Windows.Forms.Label wavLabel;
        private System.Windows.Forms.TextBox wavPath;
        private System.Windows.Forms.Button browseBtn;
        private System.Windows.Forms.RadioButton replaceRadio;
        private System.Windows.Forms.TableLayoutPanel searchRow;
        private System.Windows.Forms.Label searchLabel;
        private System.Windows.Forms.TextBox search;
        private System.Windows.Forms.CheckBox onlyBank;
        private System.Windows.Forms.ListView list;
        private System.Windows.Forms.ColumnHeader soundColumn;
        private System.Windows.Forms.ColumnHeader idColumn;
        private System.Windows.Forms.ColumnHeader keptInColumn;
        private System.Windows.Forms.ColumnHeader othersColumn;
        private System.Windows.Forms.FlowLayoutPanel streamRow;
        private System.Windows.Forms.RadioButton streamRadio;
        private System.Windows.Forms.TextBox streamId;
        private System.Windows.Forms.FlowLayoutPanel optionsRow;
        private System.Windows.Forms.Label channelsLabel;
        private System.Windows.Forms.ComboBox channels;
        private System.Windows.Forms.Label rateLabel;
        private System.Windows.Forms.ComboBox rate;
        private System.Windows.Forms.Label qualityLabel;
        private System.Windows.Forms.NumericUpDown quality;
        private System.Windows.Forms.Label gainLabel;
        private System.Windows.Forms.NumericUpDown gain;
        private System.Windows.Forms.CheckBox normalise;
        private System.Windows.Forms.TextBox summary;
        private System.Windows.Forms.FlowLayoutPanel buttonRow;
        private System.Windows.Forms.Button cancelBtn;
        private System.Windows.Forms.Button importBtn;
        private System.Windows.Forms.Button stopBtn;
        private System.Windows.Forms.Button playNewBtn;
        private System.Windows.Forms.Button playOriginalBtn;
        private System.Windows.Forms.ToolTip tips;
        private System.Windows.Forms.Timer debounce;
    }
}

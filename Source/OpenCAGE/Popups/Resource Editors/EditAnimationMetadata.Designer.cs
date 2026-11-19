namespace OpenCAGE
{
    partial class EditAnimationMetadata
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
            this.split = new System.Windows.Forms.SplitContainer();
            this.tracksBox = new System.Windows.Forms.GroupBox();
            this.trackList = new System.Windows.Forms.ListView();
            this.trackColumn = new System.Windows.Forms.ColumnHeader();
            this.trackEntriesColumn = new System.Windows.Forms.ColumnHeader();
            this.trackFiresColumn = new System.Windows.Forms.ColumnHeader();
            this.trackRow = new System.Windows.Forms.FlowLayoutPanel();
            this.trackNameBox = new System.Windows.Forms.TextBox();
            this.addTrackBtn = new System.Windows.Forms.Button();
            this.renameTrackBtn = new System.Windows.Forms.Button();
            this.removeTrackBtn = new System.Windows.Forms.Button();
            this.entriesBox = new System.Windows.Forms.GroupBox();
            this.entryList = new System.Windows.Forms.ListView();
            this.entryTimeColumn = new System.Windows.Forms.ColumnHeader();
            this.entryFiresColumn = new System.Windows.Forms.ColumnHeader();
            this.entryWhichColumn = new System.Windows.Forms.ColumnHeader();
            this.entryRow = new System.Windows.Forms.FlowLayoutPanel();
            this.timeLabel = new System.Windows.Forms.Label();
            this.timeBox = new System.Windows.Forms.TextBox();
            this.firesLabel = new System.Windows.Forms.Label();
            this.firesBox = new System.Windows.Forms.ComboBox();
            this.addEntryBtn = new System.Windows.Forms.Button();
            this.applyEntryBtn = new System.Windows.Forms.Button();
            this.removeEntryBtn = new System.Windows.Forms.Button();
            this.settingsBox = new System.Windows.Forms.GroupBox();
            this.settingList = new System.Windows.Forms.ListView();
            this.settingNameColumn = new System.Windows.Forms.ColumnHeader();
            this.settingTypeColumn = new System.Windows.Forms.ColumnHeader();
            this.settingValueColumn = new System.Windows.Forms.ColumnHeader();
            this.settingMirrorsColumn = new System.Windows.Forms.ColumnHeader();
            this.settingSpeedColumn = new System.Windows.Forms.ColumnHeader();
            this.settingConvertColumn = new System.Windows.Forms.ColumnHeader();
            this.settingRow2 = new System.Windows.Forms.FlowLayoutPanel();
            this.valueLabel = new System.Windows.Forms.Label();
            this.valueBox = new System.Windows.Forms.TextBox();
            this.addSettingBtn = new System.Windows.Forms.Button();
            this.applySettingBtn = new System.Windows.Forms.Button();
            this.removeSettingBtn = new System.Windows.Forms.Button();
            this.settingRow1 = new System.Windows.Forms.FlowLayoutPanel();
            this.settingLabel = new System.Windows.Forms.Label();
            this.settingNameBox = new System.Windows.Forms.TextBox();
            this.typeLabel = new System.Windows.Forms.Label();
            this.typeBox = new System.Windows.Forms.ComboBox();
            this.mirrorCheck = new System.Windows.Forms.CheckBox();
            this.speedCheck = new System.Windows.Forms.CheckBox();
            this.bottom = new System.Windows.Forms.Panel();
            this.statusLabel = new System.Windows.Forms.Label();
            this.buttons = new System.Windows.Forms.FlowLayoutPanel();
            this.saveBtn = new System.Windows.Forms.Button();
            this.cancelBtn = new System.Windows.Forms.Button();
            this.blockRow = new System.Windows.Forms.FlowLayoutPanel();
            this.blockLabel = new System.Windows.Forms.Label();
            this.blockBox = new System.Windows.Forms.ComboBox();
            this.infoLabel = new System.Windows.Forms.Label();
            this._tips = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.split)).BeginInit();
            this.split.Panel1.SuspendLayout();
            this.split.Panel2.SuspendLayout();
            this.split.SuspendLayout();
            this.tracksBox.SuspendLayout();
            this.trackRow.SuspendLayout();
            this.entriesBox.SuspendLayout();
            this.entryRow.SuspendLayout();
            this.settingsBox.SuspendLayout();
            this.settingRow2.SuspendLayout();
            this.settingRow1.SuspendLayout();
            this.bottom.SuspendLayout();
            this.buttons.SuspendLayout();
            this.blockRow.SuspendLayout();
            this.SuspendLayout();
            //
            // split
            //
            this.split.Dock = System.Windows.Forms.DockStyle.Fill;
            this.split.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.split.Location = new System.Drawing.Point(0, 90);
            this.split.Name = "split";
            //
            // split.Panel1
            //
            this.split.Panel1.Controls.Add(this.tracksBox);
            //
            // split.Panel2
            //
            this.split.Panel2.Controls.Add(this.entriesBox);
            this.split.Size = new System.Drawing.Size(1000, 298);
            this.split.SplitterDistance = 350;
            this.split.TabIndex = 0;
            //
            // tracksBox
            //
            this.tracksBox.Controls.Add(this.trackList);
            this.tracksBox.Controls.Add(this.trackRow);
            this.tracksBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tracksBox.Location = new System.Drawing.Point(0, 0);
            this.tracksBox.Name = "tracksBox";
            this.tracksBox.Padding = new System.Windows.Forms.Padding(6);
            this.tracksBox.Size = new System.Drawing.Size(350, 298);
            this.tracksBox.TabIndex = 0;
            this.tracksBox.TabStop = false;
            this.tracksBox.Text = "Tracks";
            //
            // trackList
            //
            this.trackList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.trackColumn,
            this.trackEntriesColumn,
            this.trackFiresColumn});
            this.trackList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.trackList.FullRowSelect = true;
            this.trackList.HideSelection = false;
            this.trackList.Location = new System.Drawing.Point(6, 19);
            this.trackList.MultiSelect = false;
            this.trackList.Name = "trackList";
            this.trackList.Size = new System.Drawing.Size(338, 241);
            this.trackList.TabIndex = 0;
            this.trackList.View = System.Windows.Forms.View.Details;
            this.trackList.SelectedIndexChanged += new System.EventHandler(this.trackList_SelectedIndexChanged);
            //
            // trackColumn
            //
            this.trackColumn.Text = "Track";
            this.trackColumn.Width = 170;
            //
            // trackEntriesColumn
            //
            this.trackEntriesColumn.Text = "Entries";
            this.trackEntriesColumn.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.trackEntriesColumn.Width = 56;
            //
            // trackFiresColumn
            //
            this.trackFiresColumn.Text = "Fires settings";
            this.trackFiresColumn.Width = 90;
            //
            // trackRow
            //
            this.trackRow.Controls.Add(this.trackNameBox);
            this.trackRow.Controls.Add(this.addTrackBtn);
            this.trackRow.Controls.Add(this.renameTrackBtn);
            this.trackRow.Controls.Add(this.removeTrackBtn);
            this.trackRow.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.trackRow.Location = new System.Drawing.Point(6, 260);
            this.trackRow.Name = "trackRow";
            this.trackRow.Padding = new System.Windows.Forms.Padding(2, 2, 2, 0);
            this.trackRow.Size = new System.Drawing.Size(338, 32);
            this.trackRow.TabIndex = 1;
            this.trackRow.WrapContents = false;
            //
            // trackNameBox
            //
            this.trackNameBox.Location = new System.Drawing.Point(5, 5);
            this.trackNameBox.Name = "trackNameBox";
            this.trackNameBox.Size = new System.Drawing.Size(130, 20);
            this.trackNameBox.TabIndex = 0;
            this._tips.SetToolTip(this.trackNameBox, "A track\'s name, as the game asks for it - e.g. AUDIO_Foley, LeftStrike, start_rag_doll.");
            //
            // addTrackBtn
            //
            this.addTrackBtn.Location = new System.Drawing.Point(141, 5);
            this.addTrackBtn.Name = "addTrackBtn";
            this.addTrackBtn.Size = new System.Drawing.Size(56, 25);
            this.addTrackBtn.TabIndex = 1;
            this.addTrackBtn.Text = "Add";
            this.addTrackBtn.UseVisualStyleBackColor = true;
            this.addTrackBtn.Click += new System.EventHandler(this.addTrackBtn_Click);
            //
            // renameTrackBtn
            //
            this.renameTrackBtn.Location = new System.Drawing.Point(203, 5);
            this.renameTrackBtn.Name = "renameTrackBtn";
            this.renameTrackBtn.Size = new System.Drawing.Size(64, 25);
            this.renameTrackBtn.TabIndex = 2;
            this.renameTrackBtn.Text = "Rename";
            this.renameTrackBtn.UseVisualStyleBackColor = true;
            this.renameTrackBtn.Click += new System.EventHandler(this.renameTrackBtn_Click);
            //
            // removeTrackBtn
            //
            this.removeTrackBtn.Location = new System.Drawing.Point(273, 5);
            this.removeTrackBtn.Name = "removeTrackBtn";
            this.removeTrackBtn.Size = new System.Drawing.Size(64, 25);
            this.removeTrackBtn.TabIndex = 3;
            this.removeTrackBtn.Text = "Remove";
            this.removeTrackBtn.UseVisualStyleBackColor = true;
            this.removeTrackBtn.Click += new System.EventHandler(this.removeTrackBtn_Click);
            //
            // entriesBox
            //
            this.entriesBox.Controls.Add(this.entryList);
            this.entriesBox.Controls.Add(this.entryRow);
            this.entriesBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.entriesBox.Location = new System.Drawing.Point(0, 0);
            this.entriesBox.Name = "entriesBox";
            this.entriesBox.Padding = new System.Windows.Forms.Padding(6);
            this.entriesBox.Size = new System.Drawing.Size(646, 298);
            this.entriesBox.TabIndex = 0;
            this.entriesBox.TabStop = false;
            this.entriesBox.Text = "Entries of the track";
            //
            // entryList
            //
            this.entryList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.entryTimeColumn,
            this.entryFiresColumn,
            this.entryWhichColumn});
            this.entryList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.entryList.FullRowSelect = true;
            this.entryList.HideSelection = false;
            this.entryList.Location = new System.Drawing.Point(6, 19);
            this.entryList.MultiSelect = false;
            this.entryList.Name = "entryList";
            this.entryList.Size = new System.Drawing.Size(634, 241);
            this.entryList.TabIndex = 0;
            this.entryList.View = System.Windows.Forms.View.Details;
            this.entryList.SelectedIndexChanged += new System.EventHandler(this.entryList_SelectedIndexChanged);
            //
            // entryTimeColumn
            //
            this.entryTimeColumn.Text = "Time (s)";
            this.entryTimeColumn.Width = 80;
            //
            // entryFiresColumn
            //
            this.entryFiresColumn.Text = "Fires";
            this.entryFiresColumn.Width = 200;
            //
            // entryWhichColumn
            //
            this.entryWhichColumn.Text = "Which is";
            this.entryWhichColumn.Width = 300;
            //
            // entryRow
            //
            this.entryRow.Controls.Add(this.timeLabel);
            this.entryRow.Controls.Add(this.timeBox);
            this.entryRow.Controls.Add(this.firesLabel);
            this.entryRow.Controls.Add(this.firesBox);
            this.entryRow.Controls.Add(this.addEntryBtn);
            this.entryRow.Controls.Add(this.applyEntryBtn);
            this.entryRow.Controls.Add(this.removeEntryBtn);
            this.entryRow.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.entryRow.Location = new System.Drawing.Point(6, 260);
            this.entryRow.Name = "entryRow";
            this.entryRow.Padding = new System.Windows.Forms.Padding(2, 2, 2, 0);
            this.entryRow.Size = new System.Drawing.Size(634, 32);
            this.entryRow.TabIndex = 1;
            this.entryRow.WrapContents = false;
            //
            // timeLabel
            //
            this.timeLabel.AutoSize = true;
            this.timeLabel.Location = new System.Drawing.Point(8, 10);
            this.timeLabel.Margin = new System.Windows.Forms.Padding(6, 8, 0, 3);
            this.timeLabel.Name = "timeLabel";
            this.timeLabel.Size = new System.Drawing.Size(30, 13);
            this.timeLabel.TabIndex = 0;
            this.timeLabel.Text = "Time";
            //
            // timeBox
            //
            this.timeBox.Location = new System.Drawing.Point(41, 5);
            this.timeBox.Name = "timeBox";
            this.timeBox.Size = new System.Drawing.Size(80, 20);
            this.timeBox.TabIndex = 1;
            this._tips.SetToolTip(this.timeBox, "Seconds from the start of the animation.");
            //
            // firesLabel
            //
            this.firesLabel.AutoSize = true;
            this.firesLabel.Location = new System.Drawing.Point(130, 10);
            this.firesLabel.Margin = new System.Windows.Forms.Padding(6, 8, 0, 3);
            this.firesLabel.Name = "firesLabel";
            this.firesLabel.Size = new System.Drawing.Size(29, 13);
            this.firesLabel.TabIndex = 2;
            this.firesLabel.Text = "Fires";
            //
            // firesBox
            //
            this.firesBox.Location = new System.Drawing.Point(162, 5);
            this.firesBox.Name = "firesBox";
            this.firesBox.Size = new System.Drawing.Size(200, 21);
            this.firesBox.TabIndex = 3;
            this._tips.SetToolTip(this.firesBox, "The setting of this block the entry fires (a sound, normally) - or nothing, for a plain moment.");
            //
            // addEntryBtn
            //
            this.addEntryBtn.Location = new System.Drawing.Point(368, 5);
            this.addEntryBtn.Name = "addEntryBtn";
            this.addEntryBtn.Size = new System.Drawing.Size(56, 25);
            this.addEntryBtn.TabIndex = 4;
            this.addEntryBtn.Text = "Add";
            this.addEntryBtn.UseVisualStyleBackColor = true;
            this.addEntryBtn.Click += new System.EventHandler(this.addEntryBtn_Click);
            //
            // applyEntryBtn
            //
            this.applyEntryBtn.Location = new System.Drawing.Point(430, 5);
            this.applyEntryBtn.Name = "applyEntryBtn";
            this.applyEntryBtn.Size = new System.Drawing.Size(60, 25);
            this.applyEntryBtn.TabIndex = 5;
            this.applyEntryBtn.Text = "Apply";
            this.applyEntryBtn.UseVisualStyleBackColor = true;
            this.applyEntryBtn.Click += new System.EventHandler(this.applyEntryBtn_Click);
            //
            // removeEntryBtn
            //
            this.removeEntryBtn.Location = new System.Drawing.Point(496, 5);
            this.removeEntryBtn.Name = "removeEntryBtn";
            this.removeEntryBtn.Size = new System.Drawing.Size(64, 25);
            this.removeEntryBtn.TabIndex = 6;
            this.removeEntryBtn.Text = "Remove";
            this.removeEntryBtn.UseVisualStyleBackColor = true;
            this.removeEntryBtn.Click += new System.EventHandler(this.removeEntryBtn_Click);
            //
            // settingsBox
            //
            this.settingsBox.Controls.Add(this.settingList);
            this.settingsBox.Controls.Add(this.settingRow2);
            this.settingsBox.Controls.Add(this.settingRow1);
            this.settingsBox.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.settingsBox.Location = new System.Drawing.Point(0, 388);
            this.settingsBox.Name = "settingsBox";
            this.settingsBox.Padding = new System.Windows.Forms.Padding(6);
            this.settingsBox.Size = new System.Drawing.Size(1000, 250);
            this.settingsBox.TabIndex = 1;
            this.settingsBox.TabStop = false;
            this.settingsBox.Text = "Settings in this block";
            //
            // settingList
            //
            this.settingList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.settingNameColumn,
            this.settingTypeColumn,
            this.settingValueColumn,
            this.settingMirrorsColumn,
            this.settingSpeedColumn,
            this.settingConvertColumn});
            this.settingList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.settingList.FullRowSelect = true;
            this.settingList.HideSelection = false;
            this.settingList.Location = new System.Drawing.Point(6, 19);
            this.settingList.MultiSelect = false;
            this.settingList.Name = "settingList";
            this.settingList.Size = new System.Drawing.Size(988, 161);
            this.settingList.TabIndex = 0;
            this.settingList.View = System.Windows.Forms.View.Details;
            this.settingList.SelectedIndexChanged += new System.EventHandler(this.settingList_SelectedIndexChanged);
            //
            // settingNameColumn
            //
            this.settingNameColumn.Text = "Setting";
            this.settingNameColumn.Width = 200;
            //
            // settingTypeColumn
            //
            this.settingTypeColumn.Text = "Type";
            this.settingTypeColumn.Width = 130;
            //
            // settingValueColumn
            //
            this.settingValueColumn.Text = "Value";
            this.settingValueColumn.Width = 400;
            //
            // settingMirrorsColumn
            //
            this.settingMirrorsColumn.Text = "Mirrors";
            //
            // settingSpeedColumn
            //
            this.settingSpeedColumn.Text = "Play speed";
            this.settingSpeedColumn.Width = 75;
            //
            // settingConvertColumn
            //
            this.settingConvertColumn.Text = "Convert";
            this.settingConvertColumn.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // settingRow2
            //
            this.settingRow2.Controls.Add(this.valueLabel);
            this.settingRow2.Controls.Add(this.valueBox);
            this.settingRow2.Controls.Add(this.addSettingBtn);
            this.settingRow2.Controls.Add(this.applySettingBtn);
            this.settingRow2.Controls.Add(this.removeSettingBtn);
            this.settingRow2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.settingRow2.Location = new System.Drawing.Point(6, 180);
            this.settingRow2.Name = "settingRow2";
            this.settingRow2.Padding = new System.Windows.Forms.Padding(2, 2, 2, 0);
            this.settingRow2.Size = new System.Drawing.Size(988, 32);
            this.settingRow2.TabIndex = 1;
            this.settingRow2.WrapContents = false;
            //
            // valueLabel
            //
            this.valueLabel.AutoSize = true;
            this.valueLabel.Location = new System.Drawing.Point(8, 10);
            this.valueLabel.Margin = new System.Windows.Forms.Padding(6, 8, 0, 3);
            this.valueLabel.Name = "valueLabel";
            this.valueLabel.Size = new System.Drawing.Size(34, 13);
            this.valueLabel.TabIndex = 0;
            this.valueLabel.Text = "Value";
            //
            // valueBox
            //
            this.valueBox.Location = new System.Drawing.Point(45, 5);
            this.valueBox.Name = "valueBox";
            this.valueBox.Size = new System.Drawing.Size(480, 20);
            this.valueBox.TabIndex = 1;
            this._tips.SetToolTip(this.valueBox, "Numbers in the form 1.5; a vector as x, y, z; a sound as [ArgumentList={},Bone={},Event={},Offset={0,0,0},UseArguments={No}] with the sound event and the bone it plays from filled in.");
            //
            // addSettingBtn
            //
            this.addSettingBtn.Location = new System.Drawing.Point(531, 5);
            this.addSettingBtn.Name = "addSettingBtn";
            this.addSettingBtn.Size = new System.Drawing.Size(56, 25);
            this.addSettingBtn.TabIndex = 2;
            this.addSettingBtn.Text = "Add";
            this.addSettingBtn.UseVisualStyleBackColor = true;
            this.addSettingBtn.Click += new System.EventHandler(this.addSettingBtn_Click);
            //
            // applySettingBtn
            //
            this.applySettingBtn.Location = new System.Drawing.Point(593, 5);
            this.applySettingBtn.Name = "applySettingBtn";
            this.applySettingBtn.Size = new System.Drawing.Size(60, 25);
            this.applySettingBtn.TabIndex = 3;
            this.applySettingBtn.Text = "Apply";
            this.applySettingBtn.UseVisualStyleBackColor = true;
            this.applySettingBtn.Click += new System.EventHandler(this.applySettingBtn_Click);
            //
            // removeSettingBtn
            //
            this.removeSettingBtn.Location = new System.Drawing.Point(659, 5);
            this.removeSettingBtn.Name = "removeSettingBtn";
            this.removeSettingBtn.Size = new System.Drawing.Size(64, 25);
            this.removeSettingBtn.TabIndex = 4;
            this.removeSettingBtn.Text = "Remove";
            this.removeSettingBtn.UseVisualStyleBackColor = true;
            this.removeSettingBtn.Click += new System.EventHandler(this.removeSettingBtn_Click);
            //
            // settingRow1
            //
            this.settingRow1.Controls.Add(this.settingLabel);
            this.settingRow1.Controls.Add(this.settingNameBox);
            this.settingRow1.Controls.Add(this.typeLabel);
            this.settingRow1.Controls.Add(this.typeBox);
            this.settingRow1.Controls.Add(this.mirrorCheck);
            this.settingRow1.Controls.Add(this.speedCheck);
            this.settingRow1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.settingRow1.Location = new System.Drawing.Point(6, 212);
            this.settingRow1.Name = "settingRow1";
            this.settingRow1.Padding = new System.Windows.Forms.Padding(2, 2, 2, 0);
            this.settingRow1.Size = new System.Drawing.Size(988, 32);
            this.settingRow1.TabIndex = 2;
            this.settingRow1.WrapContents = false;
            //
            // settingLabel
            //
            this.settingLabel.AutoSize = true;
            this.settingLabel.Location = new System.Drawing.Point(8, 10);
            this.settingLabel.Margin = new System.Windows.Forms.Padding(6, 8, 0, 3);
            this.settingLabel.Name = "settingLabel";
            this.settingLabel.Size = new System.Drawing.Size(40, 13);
            this.settingLabel.TabIndex = 0;
            this.settingLabel.Text = "Setting";
            //
            // settingNameBox
            //
            this.settingNameBox.Location = new System.Drawing.Point(51, 5);
            this.settingNameBox.Name = "settingNameBox";
            this.settingNameBox.Size = new System.Drawing.Size(200, 20);
            this.settingNameBox.TabIndex = 1;
            this._tips.SetToolTip(this.settingNameBox, "The setting\'s name, which a track\'s entries name to fire it.");
            //
            // typeLabel
            //
            this.typeLabel.AutoSize = true;
            this.typeLabel.Location = new System.Drawing.Point(260, 10);
            this.typeLabel.Margin = new System.Windows.Forms.Padding(6, 8, 0, 3);
            this.typeLabel.Name = "typeLabel";
            this.typeLabel.Size = new System.Drawing.Size(31, 13);
            this.typeLabel.TabIndex = 2;
            this.typeLabel.Text = "Type";
            //
            // typeBox
            //
            this.typeBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.typeBox.Location = new System.Drawing.Point(294, 5);
            this.typeBox.Name = "typeBox";
            this.typeBox.Size = new System.Drawing.Size(150, 21);
            this.typeBox.TabIndex = 3;
            this.typeBox.SelectedIndexChanged += new System.EventHandler(this.typeBox_SelectedIndexChanged);
            //
            // mirrorCheck
            //
            this.mirrorCheck.AutoSize = true;
            this.mirrorCheck.Location = new System.Drawing.Point(455, 9);
            this.mirrorCheck.Margin = new System.Windows.Forms.Padding(8, 7, 3, 3);
            this.mirrorCheck.Name = "mirrorCheck";
            this.mirrorCheck.Size = new System.Drawing.Size(73, 17);
            this.mirrorCheck.TabIndex = 4;
            this.mirrorCheck.Text = "Can mirror";
            this._tips.SetToolTip(this.mirrorCheck, "Stored with the setting as the game\'s own flag: whether its value follows a mirrored play.");
            //
            // speedCheck
            //
            this.speedCheck.AutoSize = true;
            this.speedCheck.Location = new System.Drawing.Point(539, 9);
            this.speedCheck.Margin = new System.Windows.Forms.Padding(8, 7, 3, 3);
            this.speedCheck.Name = "speedCheck";
            this.speedCheck.Size = new System.Drawing.Size(134, 17);
            this.speedCheck.TabIndex = 5;
            this.speedCheck.Text = "Scales with play speed";
            this._tips.SetToolTip(this.speedCheck, "Stored with the setting as the game\'s own flag: whether its value scales with how fast the animation plays.");
            //
            // bottom
            //
            this.bottom.Controls.Add(this.statusLabel);
            this.bottom.Controls.Add(this.buttons);
            this.bottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottom.Location = new System.Drawing.Point(0, 638);
            this.bottom.Name = "bottom";
            this.bottom.Size = new System.Drawing.Size(1000, 42);
            this.bottom.TabIndex = 2;
            //
            // statusLabel
            //
            this.statusLabel.AutoEllipsis = true;
            this.statusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.statusLabel.Location = new System.Drawing.Point(0, 0);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Padding = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this.statusLabel.Size = new System.Drawing.Size(776, 42);
            this.statusLabel.TabIndex = 0;
            this.statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // buttons
            //
            this.buttons.Controls.Add(this.saveBtn);
            this.buttons.Controls.Add(this.cancelBtn);
            this.buttons.Dock = System.Windows.Forms.DockStyle.Right;
            this.buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.buttons.Location = new System.Drawing.Point(776, 0);
            this.buttons.Name = "buttons";
            this.buttons.Padding = new System.Windows.Forms.Padding(0, 6, 6, 0);
            this.buttons.Size = new System.Drawing.Size(224, 42);
            this.buttons.TabIndex = 1;
            this.buttons.WrapContents = false;
            //
            // saveBtn
            //
            this.saveBtn.Location = new System.Drawing.Point(115, 9);
            this.saveBtn.Name = "saveBtn";
            this.saveBtn.Size = new System.Drawing.Size(100, 25);
            this.saveBtn.TabIndex = 0;
            this.saveBtn.Text = "Save";
            this._tips.SetToolTip(this.saveBtn, "Put these changes into the animation. ANIMATION.PAK is written straight away.");
            this.saveBtn.UseVisualStyleBackColor = true;
            this.saveBtn.Click += new System.EventHandler(this.saveBtn_Click);
            //
            // cancelBtn
            //
            this.cancelBtn.Location = new System.Drawing.Point(9, 9);
            this.cancelBtn.Name = "cancelBtn";
            this.cancelBtn.Size = new System.Drawing.Size(100, 25);
            this.cancelBtn.TabIndex = 1;
            this.cancelBtn.Text = "Cancel";
            this.cancelBtn.UseVisualStyleBackColor = true;
            this.cancelBtn.Click += new System.EventHandler(this.cancelBtn_Click);
            //
            // blockRow
            //
            this.blockRow.Controls.Add(this.blockLabel);
            this.blockRow.Controls.Add(this.blockBox);
            this.blockRow.Dock = System.Windows.Forms.DockStyle.Top;
            this.blockRow.Location = new System.Drawing.Point(0, 58);
            this.blockRow.Name = "blockRow";
            this.blockRow.Padding = new System.Windows.Forms.Padding(2, 2, 2, 0);
            this.blockRow.Size = new System.Drawing.Size(1000, 32);
            this.blockRow.TabIndex = 3;
            this.blockRow.WrapContents = false;
            //
            // blockLabel
            //
            this.blockLabel.AutoSize = true;
            this.blockLabel.Location = new System.Drawing.Point(8, 10);
            this.blockLabel.Margin = new System.Windows.Forms.Padding(6, 8, 0, 3);
            this.blockLabel.Name = "blockLabel";
            this.blockLabel.Size = new System.Drawing.Size(34, 13);
            this.blockLabel.TabIndex = 0;
            this.blockLabel.Text = "Block";
            //
            // blockBox
            //
            this.blockBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.blockBox.Location = new System.Drawing.Point(45, 5);
            this.blockBox.Name = "blockBox";
            this.blockBox.Size = new System.Drawing.Size(420, 21);
            this.blockBox.TabIndex = 1;
            this._tips.SetToolTip(this.blockBox, "The animation\'s own block holds what goes for it however it plays; each use has a block of its own tags.");
            this.blockBox.SelectedIndexChanged += new System.EventHandler(this.blockBox_SelectedIndexChanged);
            //
            // infoLabel
            //
            this.infoLabel.AutoEllipsis = true;
            this.infoLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this.infoLabel.Location = new System.Drawing.Point(0, 0);
            this.infoLabel.Name = "infoLabel";
            this.infoLabel.Padding = new System.Windows.Forms.Padding(8, 8, 8, 0);
            this.infoLabel.Size = new System.Drawing.Size(1000, 58);
            this.infoLabel.TabIndex = 4;
            //
            // EditAnimationMetadata
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1000, 680);
            this.Controls.Add(this.split);
            this.Controls.Add(this.settingsBox);
            this.Controls.Add(this.bottom);
            this.Controls.Add(this.blockRow);
            this.Controls.Add(this.infoLabel);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(920, 580);
            this.Name = "EditAnimationMetadata";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.EditAnimationMetadata_FormClosing);
            this.split.Panel1.ResumeLayout(false);
            this.split.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.split)).EndInit();
            this.split.ResumeLayout(false);
            this.tracksBox.ResumeLayout(false);
            this.trackRow.ResumeLayout(false);
            this.trackRow.PerformLayout();
            this.entriesBox.ResumeLayout(false);
            this.entryRow.ResumeLayout(false);
            this.entryRow.PerformLayout();
            this.settingsBox.ResumeLayout(false);
            this.settingRow2.ResumeLayout(false);
            this.settingRow2.PerformLayout();
            this.settingRow1.ResumeLayout(false);
            this.settingRow1.PerformLayout();
            this.bottom.ResumeLayout(false);
            this.buttons.ResumeLayout(false);
            this.blockRow.ResumeLayout(false);
            this.blockRow.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer split;
        private System.Windows.Forms.GroupBox tracksBox;
        private System.Windows.Forms.ListView trackList;
        private System.Windows.Forms.ColumnHeader trackColumn;
        private System.Windows.Forms.ColumnHeader trackEntriesColumn;
        private System.Windows.Forms.ColumnHeader trackFiresColumn;
        private System.Windows.Forms.FlowLayoutPanel trackRow;
        private System.Windows.Forms.TextBox trackNameBox;
        private System.Windows.Forms.Button addTrackBtn;
        private System.Windows.Forms.Button renameTrackBtn;
        private System.Windows.Forms.Button removeTrackBtn;
        private System.Windows.Forms.GroupBox entriesBox;
        private System.Windows.Forms.ListView entryList;
        private System.Windows.Forms.ColumnHeader entryTimeColumn;
        private System.Windows.Forms.ColumnHeader entryFiresColumn;
        private System.Windows.Forms.ColumnHeader entryWhichColumn;
        private System.Windows.Forms.FlowLayoutPanel entryRow;
        private System.Windows.Forms.Label timeLabel;
        private System.Windows.Forms.TextBox timeBox;
        private System.Windows.Forms.Label firesLabel;
        private System.Windows.Forms.ComboBox firesBox;
        private System.Windows.Forms.Button addEntryBtn;
        private System.Windows.Forms.Button applyEntryBtn;
        private System.Windows.Forms.Button removeEntryBtn;
        private System.Windows.Forms.GroupBox settingsBox;
        private System.Windows.Forms.ListView settingList;
        private System.Windows.Forms.ColumnHeader settingNameColumn;
        private System.Windows.Forms.ColumnHeader settingTypeColumn;
        private System.Windows.Forms.ColumnHeader settingValueColumn;
        private System.Windows.Forms.ColumnHeader settingMirrorsColumn;
        private System.Windows.Forms.ColumnHeader settingSpeedColumn;
        private System.Windows.Forms.ColumnHeader settingConvertColumn;
        private System.Windows.Forms.FlowLayoutPanel settingRow2;
        private System.Windows.Forms.Label valueLabel;
        private System.Windows.Forms.TextBox valueBox;
        private System.Windows.Forms.Button addSettingBtn;
        private System.Windows.Forms.Button applySettingBtn;
        private System.Windows.Forms.Button removeSettingBtn;
        private System.Windows.Forms.FlowLayoutPanel settingRow1;
        private System.Windows.Forms.Label settingLabel;
        private System.Windows.Forms.TextBox settingNameBox;
        private System.Windows.Forms.Label typeLabel;
        private System.Windows.Forms.ComboBox typeBox;
        private System.Windows.Forms.CheckBox mirrorCheck;
        private System.Windows.Forms.CheckBox speedCheck;
        private System.Windows.Forms.Panel bottom;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.FlowLayoutPanel buttons;
        private System.Windows.Forms.Button saveBtn;
        private System.Windows.Forms.Button cancelBtn;
        private System.Windows.Forms.FlowLayoutPanel blockRow;
        private System.Windows.Forms.Label blockLabel;
        private System.Windows.Forms.ComboBox blockBox;
        private System.Windows.Forms.Label infoLabel;
        private System.Windows.Forms.ToolTip _tips;
    }
}

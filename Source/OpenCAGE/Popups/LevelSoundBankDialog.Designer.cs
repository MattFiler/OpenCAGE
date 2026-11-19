namespace OpenCAGE
{
    partial class LevelSoundBankDialog
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
            this.nameRow = new System.Windows.Forms.TableLayoutPanel();
            this.bankNameLabel = new System.Windows.Forms.Label();
            this.bankName = new System.Windows.Forms.TextBox();
            this.eventsLabel = new System.Windows.Forms.Label();
            this.events = new System.Windows.Forms.ListView();
            this.eventColumn = new System.Windows.Forms.ColumnHeader();
            this.wavColumn = new System.Windows.Forms.ColumnHeader();
            this.soundsLikeColumn = new System.Windows.Forms.ColumnHeader();
            this.loopsColumn = new System.Windows.Forms.ColumnHeader();
            this.eventButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.removeEventBtn = new System.Windows.Forms.Button();
            this.addGroup = new System.Windows.Forms.GroupBox();
            this.newEvent = new System.Windows.Forms.TableLayoutPanel();
            this.eventNameRow = new System.Windows.Forms.TableLayoutPanel();
            this.eventNameLabel = new System.Windows.Forms.Label();
            this.eventName = new System.Windows.Forms.TextBox();
            this.wavRow = new System.Windows.Forms.TableLayoutPanel();
            this.wavLabel = new System.Windows.Forms.Label();
            this.wavPath = new System.Windows.Forms.TextBox();
            this.browseBtn = new System.Windows.Forms.Button();
            this.searchRow = new System.Windows.Forms.TableLayoutPanel();
            this.searchLabel = new System.Windows.Forms.Label();
            this.search = new System.Windows.Forms.TextBox();
            this.sounds = new System.Windows.Forms.ListView();
            this.soundColumn = new System.Windows.Forms.ColumnHeader();
            this.idColumn = new System.Windows.Forms.ColumnHeader();
            this.keptInColumn = new System.Windows.Forms.ColumnHeader();
            this.addRow = new System.Windows.Forms.FlowLayoutPanel();
            this.loop = new System.Windows.Forms.CheckBox();
            this.playBtn = new System.Windows.Forms.Button();
            this.stopBtn = new System.Windows.Forms.Button();
            this.addEventBtn = new System.Windows.Forms.Button();
            this.summary = new System.Windows.Forms.TextBox();
            this.buttonRow = new System.Windows.Forms.FlowLayoutPanel();
            this.cancelBtn = new System.Windows.Forms.Button();
            this.saveBtn = new System.Windows.Forms.Button();
            this.tips = new System.Windows.Forms.ToolTip(this.components);
            this.root.SuspendLayout();
            this.nameRow.SuspendLayout();
            this.eventButtons.SuspendLayout();
            this.addGroup.SuspendLayout();
            this.newEvent.SuspendLayout();
            this.eventNameRow.SuspendLayout();
            this.wavRow.SuspendLayout();
            this.searchRow.SuspendLayout();
            this.addRow.SuspendLayout();
            this.buttonRow.SuspendLayout();
            this.SuspendLayout();
            //
            // root
            //
            this.root.ColumnCount = 1;
            this.root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.root.Controls.Add(this.nameRow, 0, 0);
            this.root.Controls.Add(this.eventsLabel, 0, 1);
            this.root.Controls.Add(this.events, 0, 2);
            this.root.Controls.Add(this.eventButtons, 0, 3);
            this.root.Controls.Add(this.addGroup, 0, 4);
            this.root.Controls.Add(this.summary, 0, 5);
            this.root.Controls.Add(this.buttonRow, 0, 6);
            this.root.Dock = System.Windows.Forms.DockStyle.Fill;
            this.root.Location = new System.Drawing.Point(0, 0);
            this.root.Name = "root";
            this.root.Padding = new System.Windows.Forms.Padding(8);
            this.root.RowCount = 7;
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 70F));
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 110F));
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.root.Size = new System.Drawing.Size(880, 720);
            this.root.TabIndex = 0;
            //
            // nameRow
            //
            this.nameRow.AutoSize = true;
            this.nameRow.ColumnCount = 3;
            this.nameRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 130F));
            this.nameRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.nameRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.nameRow.Controls.Add(this.bankNameLabel, 0, 0);
            this.nameRow.Controls.Add(this.bankName, 1, 0);
            this.nameRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.nameRow.Location = new System.Drawing.Point(8, 8);
            this.nameRow.Margin = new System.Windows.Forms.Padding(0);
            this.nameRow.Name = "nameRow";
            this.nameRow.RowCount = 1;
            this.nameRow.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.nameRow.Size = new System.Drawing.Size(864, 26);
            this.nameRow.TabIndex = 0;
            //
            // bankNameLabel
            //
            this.bankNameLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.bankNameLabel.AutoSize = true;
            this.bankNameLabel.Location = new System.Drawing.Point(3, 8);
            this.bankNameLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.bankNameLabel.Name = "bankNameLabel";
            this.bankNameLabel.Size = new System.Drawing.Size(94, 13);
            this.bankNameLabel.TabIndex = 0;
            this.bankNameLabel.Text = "Soundbank name:";
            //
            // bankName
            //
            this.bankName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.bankName.Location = new System.Drawing.Point(133, 3);
            this.bankName.Name = "bankName";
            this.bankName.Size = new System.Drawing.Size(728, 20);
            this.bankName.TabIndex = 1;
            this.tips.SetToolTip(this.bankName, "The soundbank's name: letters, digits and underscores. A SoundLoadBank loads it by this name (a VariableString holding it, linked to the loader's sound_bank pin).");
            //
            // eventsLabel
            //
            this.eventsLabel.AutoSize = true;
            this.eventsLabel.Location = new System.Drawing.Point(11, 42);
            this.eventsLabel.Margin = new System.Windows.Forms.Padding(3, 8, 3, 0);
            this.eventsLabel.Name = "eventsLabel";
            this.eventsLabel.Size = new System.Drawing.Size(56, 13);
            this.eventsLabel.TabIndex = 1;
            this.eventsLabel.Text = "Its events:";
            //
            // events
            //
            this.events.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.eventColumn,
            this.wavColumn,
            this.soundsLikeColumn,
            this.loopsColumn});
            this.events.Dock = System.Windows.Forms.DockStyle.Fill;
            this.events.FullRowSelect = true;
            this.events.HideSelection = false;
            this.events.Location = new System.Drawing.Point(11, 58);
            this.events.Name = "events";
            this.events.Size = new System.Drawing.Size(858, 137);
            this.events.TabIndex = 2;
            this.events.View = System.Windows.Forms.View.Details;
            this.events.SelectedIndexChanged += new System.EventHandler(this.events_SelectedIndexChanged);
            //
            // eventColumn
            //
            this.eventColumn.Text = "Event";
            this.eventColumn.Width = 260;
            //
            // wavColumn
            //
            this.wavColumn.Text = ".wav";
            this.wavColumn.Width = 200;
            //
            // soundsLikeColumn
            //
            this.soundsLikeColumn.Text = "Sounds like";
            this.soundsLikeColumn.Width = 300;
            //
            // loopsColumn
            //
            this.loopsColumn.Text = "Loops";
            this.loopsColumn.Width = 60;
            //
            // eventButtons
            //
            this.eventButtons.AutoSize = true;
            this.eventButtons.Controls.Add(this.removeEventBtn);
            this.eventButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.eventButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.eventButtons.Location = new System.Drawing.Point(8, 198);
            this.eventButtons.Margin = new System.Windows.Forms.Padding(0);
            this.eventButtons.Name = "eventButtons";
            this.eventButtons.Size = new System.Drawing.Size(864, 32);
            this.eventButtons.TabIndex = 3;
            this.eventButtons.WrapContents = false;
            //
            // removeEventBtn
            //
            this.removeEventBtn.Enabled = false;
            this.removeEventBtn.Location = new System.Drawing.Point(751, 3);
            this.removeEventBtn.Name = "removeEventBtn";
            this.removeEventBtn.Size = new System.Drawing.Size(110, 26);
            this.removeEventBtn.TabIndex = 0;
            this.removeEventBtn.Text = "Remove Event";
            this.removeEventBtn.UseVisualStyleBackColor = true;
            this.removeEventBtn.Click += new System.EventHandler(this.removeEventBtn_Click);
            //
            // addGroup
            //
            this.addGroup.Controls.Add(this.newEvent);
            this.addGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.addGroup.Location = new System.Drawing.Point(11, 233);
            this.addGroup.Name = "addGroup";
            this.addGroup.Padding = new System.Windows.Forms.Padding(6);
            this.addGroup.Size = new System.Drawing.Size(858, 329);
            this.addGroup.TabIndex = 4;
            this.addGroup.TabStop = false;
            this.addGroup.Text = "Add an event";
            //
            // newEvent
            //
            this.newEvent.ColumnCount = 1;
            this.newEvent.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.newEvent.Controls.Add(this.eventNameRow, 0, 0);
            this.newEvent.Controls.Add(this.wavRow, 0, 1);
            this.newEvent.Controls.Add(this.searchRow, 0, 2);
            this.newEvent.Controls.Add(this.sounds, 0, 3);
            this.newEvent.Controls.Add(this.addRow, 0, 4);
            this.newEvent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.newEvent.Location = new System.Drawing.Point(6, 19);
            this.newEvent.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this.newEvent.Name = "newEvent";
            this.newEvent.RowCount = 5;
            this.newEvent.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.newEvent.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.newEvent.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.newEvent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.newEvent.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.newEvent.Size = new System.Drawing.Size(846, 304);
            this.newEvent.TabIndex = 0;
            //
            // eventNameRow
            //
            this.eventNameRow.AutoSize = true;
            this.eventNameRow.ColumnCount = 3;
            this.eventNameRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 130F));
            this.eventNameRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.eventNameRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.eventNameRow.Controls.Add(this.eventNameLabel, 0, 0);
            this.eventNameRow.Controls.Add(this.eventName, 1, 0);
            this.eventNameRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.eventNameRow.Location = new System.Drawing.Point(0, 0);
            this.eventNameRow.Margin = new System.Windows.Forms.Padding(0);
            this.eventNameRow.Name = "eventNameRow";
            this.eventNameRow.RowCount = 1;
            this.eventNameRow.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.eventNameRow.Size = new System.Drawing.Size(846, 26);
            this.eventNameRow.TabIndex = 0;
            //
            // eventNameLabel
            //
            this.eventNameLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.eventNameLabel.AutoSize = true;
            this.eventNameLabel.Location = new System.Drawing.Point(3, 8);
            this.eventNameLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.eventNameLabel.Name = "eventNameLabel";
            this.eventNameLabel.Size = new System.Drawing.Size(98, 13);
            this.eventNameLabel.TabIndex = 0;
            this.eventNameLabel.Text = "New event's name:";
            //
            // eventName
            //
            this.eventName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.eventName.Location = new System.Drawing.Point(133, 3);
            this.eventName.Name = "eventName";
            this.eventName.Size = new System.Drawing.Size(710, 20);
            this.eventName.TabIndex = 1;
            this.tips.SetToolTip(this.eventName, "The sound event's name, as a Sound entity's sound_event names it: letters, digits and underscores, and not a name the game already uses.");
            //
            // wavRow
            //
            this.wavRow.AutoSize = true;
            this.wavRow.ColumnCount = 3;
            this.wavRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 130F));
            this.wavRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.wavRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.wavRow.Controls.Add(this.wavLabel, 0, 0);
            this.wavRow.Controls.Add(this.wavPath, 1, 0);
            this.wavRow.Controls.Add(this.browseBtn, 2, 0);
            this.wavRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.wavRow.Location = new System.Drawing.Point(0, 26);
            this.wavRow.Margin = new System.Windows.Forms.Padding(0);
            this.wavRow.Name = "wavRow";
            this.wavRow.RowCount = 1;
            this.wavRow.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.wavRow.Size = new System.Drawing.Size(846, 30);
            this.wavRow.TabIndex = 1;
            //
            // wavLabel
            //
            this.wavLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.wavLabel.AutoSize = true;
            this.wavLabel.Location = new System.Drawing.Point(3, 10);
            this.wavLabel.Margin = new System.Windows.Forms.Padding(3, 7, 3, 3);
            this.wavLabel.Name = "wavLabel";
            this.wavLabel.Size = new System.Drawing.Size(82, 13);
            this.wavLabel.TabIndex = 0;
            this.wavLabel.Text = "Its audio (.wav):";
            //
            // wavPath
            //
            this.wavPath.Dock = System.Windows.Forms.DockStyle.Fill;
            this.wavPath.Location = new System.Drawing.Point(133, 3);
            this.wavPath.Name = "wavPath";
            this.wavPath.ReadOnly = true;
            this.wavPath.Size = new System.Drawing.Size(614, 20);
            this.wavPath.TabIndex = 1;
            //
            // browseBtn
            //
            this.browseBtn.Location = new System.Drawing.Point(753, 3);
            this.browseBtn.Name = "browseBtn";
            this.browseBtn.Size = new System.Drawing.Size(90, 24);
            this.browseBtn.TabIndex = 2;
            this.browseBtn.Text = "Browse...";
            this.browseBtn.UseVisualStyleBackColor = true;
            this.browseBtn.Click += new System.EventHandler(this.browseBtn_Click);
            //
            // searchRow
            //
            this.searchRow.AutoSize = true;
            this.searchRow.ColumnCount = 3;
            this.searchRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 130F));
            this.searchRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.searchRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.searchRow.Controls.Add(this.searchLabel, 0, 0);
            this.searchRow.Controls.Add(this.search, 1, 0);
            this.searchRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.searchRow.Location = new System.Drawing.Point(0, 56);
            this.searchRow.Margin = new System.Windows.Forms.Padding(0);
            this.searchRow.Name = "searchRow";
            this.searchRow.RowCount = 1;
            this.searchRow.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.searchRow.Size = new System.Drawing.Size(846, 26);
            this.searchRow.TabIndex = 2;
            //
            // searchLabel
            //
            this.searchLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.searchLabel.AutoSize = true;
            this.searchLabel.Location = new System.Drawing.Point(3, 8);
            this.searchLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.searchLabel.Name = "searchLabel";
            this.searchLabel.Size = new System.Drawing.Size(106, 13);
            this.searchLabel.TabIndex = 0;
            this.searchLabel.Text = "Sounds like (search):";
            //
            // search
            //
            this.search.Dock = System.Windows.Forms.DockStyle.Fill;
            this.search.Location = new System.Drawing.Point(133, 3);
            this.search.Name = "search";
            this.search.Size = new System.Drawing.Size(710, 20);
            this.search.TabIndex = 1;
            this.search.TextChanged += new System.EventHandler(this.search_TextChanged);
            //
            // sounds
            //
            this.sounds.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.soundColumn,
            this.idColumn,
            this.keptInColumn});
            this.sounds.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sounds.FullRowSelect = true;
            this.sounds.HideSelection = false;
            this.sounds.Location = new System.Drawing.Point(3, 85);
            this.sounds.MultiSelect = false;
            this.sounds.Name = "sounds";
            this.sounds.Size = new System.Drawing.Size(840, 184);
            this.sounds.TabIndex = 3;
            this.tips.SetToolTip(this.sounds, "The sound of the game's this one is made to sound like: its actor-mixer, output bus, 3D positioning and distance attenuation, reverb sends and volume are borrowed. The .wav is laid out like it (channels and sample rate).");
            this.sounds.View = System.Windows.Forms.View.Details;
            this.sounds.SelectedIndexChanged += new System.EventHandler(this.sounds_SelectedIndexChanged);
            //
            // soundColumn
            //
            this.soundColumn.Text = "Sound";
            this.soundColumn.Width = 330;
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
            // addRow
            //
            this.addRow.AutoSize = true;
            this.addRow.Controls.Add(this.loop);
            this.addRow.Controls.Add(this.playBtn);
            this.addRow.Controls.Add(this.stopBtn);
            this.addRow.Controls.Add(this.addEventBtn);
            this.addRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.addRow.Location = new System.Drawing.Point(0, 272);
            this.addRow.Margin = new System.Windows.Forms.Padding(0);
            this.addRow.Name = "addRow";
            this.addRow.Size = new System.Drawing.Size(846, 32);
            this.addRow.TabIndex = 4;
            this.addRow.WrapContents = false;
            //
            // loop
            //
            this.loop.AutoSize = true;
            this.loop.Location = new System.Drawing.Point(3, 7);
            this.loop.Margin = new System.Windows.Forms.Padding(3, 7, 12, 3);
            this.loop.Name = "loop";
            this.loop.Size = new System.Drawing.Size(113, 17);
            this.loop.TabIndex = 0;
            this.loop.Text = "Loop until stopped";
            this.tips.SetToolTip(this.loop, "Repeat the sound until something stops it - the whole .wav, or its own loop region if it has one (a smpl chunk, as audio editors write).");
            this.loop.UseVisualStyleBackColor = true;
            //
            // playBtn
            //
            this.playBtn.Enabled = false;
            this.playBtn.Location = new System.Drawing.Point(131, 3);
            this.playBtn.Name = "playBtn";
            this.playBtn.Size = new System.Drawing.Size(90, 26);
            this.playBtn.TabIndex = 1;
            this.playBtn.Text = "Play Sound";
            this.tips.SetToolTip(this.playBtn, "Play the selected sound of the game's.");
            this.playBtn.UseVisualStyleBackColor = true;
            this.playBtn.Click += new System.EventHandler(this.playBtn_Click);
            //
            // stopBtn
            //
            this.stopBtn.Enabled = false;
            this.stopBtn.Location = new System.Drawing.Point(227, 3);
            this.stopBtn.Name = "stopBtn";
            this.stopBtn.Size = new System.Drawing.Size(60, 26);
            this.stopBtn.TabIndex = 2;
            this.stopBtn.Text = "Stop";
            this.stopBtn.UseVisualStyleBackColor = true;
            this.stopBtn.Click += new System.EventHandler(this.stopBtn_Click);
            //
            // addEventBtn
            //
            this.addEventBtn.Location = new System.Drawing.Point(293, 3);
            this.addEventBtn.Name = "addEventBtn";
            this.addEventBtn.Size = new System.Drawing.Size(100, 26);
            this.addEventBtn.TabIndex = 3;
            this.addEventBtn.Text = "Add Event";
            this.addEventBtn.UseVisualStyleBackColor = true;
            this.addEventBtn.Click += new System.EventHandler(this.addEventBtn_Click);
            //
            // summary
            //
            this.summary.Dock = System.Windows.Forms.DockStyle.Fill;
            this.summary.Location = new System.Drawing.Point(11, 571);
            this.summary.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.summary.Multiline = true;
            this.summary.Name = "summary";
            this.summary.ReadOnly = true;
            this.summary.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.summary.Size = new System.Drawing.Size(858, 101);
            this.summary.TabIndex = 5;
            //
            // buttonRow
            //
            this.buttonRow.AutoSize = true;
            this.buttonRow.Controls.Add(this.cancelBtn);
            this.buttonRow.Controls.Add(this.saveBtn);
            this.buttonRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonRow.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.buttonRow.Location = new System.Drawing.Point(8, 679);
            this.buttonRow.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.buttonRow.Name = "buttonRow";
            this.buttonRow.Size = new System.Drawing.Size(864, 33);
            this.buttonRow.TabIndex = 6;
            this.buttonRow.WrapContents = false;
            //
            // cancelBtn
            //
            this.cancelBtn.Location = new System.Drawing.Point(771, 3);
            this.cancelBtn.Name = "cancelBtn";
            this.cancelBtn.Size = new System.Drawing.Size(90, 26);
            this.cancelBtn.TabIndex = 0;
            this.cancelBtn.Text = "Cancel";
            this.cancelBtn.UseVisualStyleBackColor = true;
            this.cancelBtn.Click += new System.EventHandler(this.cancelBtn_Click);
            //
            // saveBtn
            //
            this.saveBtn.Enabled = false;
            this.saveBtn.Location = new System.Drawing.Point(675, 3);
            this.saveBtn.Name = "saveBtn";
            this.saveBtn.Size = new System.Drawing.Size(90, 26);
            this.saveBtn.TabIndex = 1;
            this.saveBtn.Text = "Create";
            this.saveBtn.UseVisualStyleBackColor = true;
            this.saveBtn.Click += new System.EventHandler(this.saveBtn_Click);
            //
            // LevelSoundBankDialog
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelBtn;
            this.ClientSize = new System.Drawing.Size(880, 720);
            this.Controls.Add(this.root);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(680, 600);
            this.Name = "LevelSoundBankDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "New Soundbank";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.LevelSoundBankDialog_FormClosed);
            this.Load += new System.EventHandler(this.LevelSoundBankDialog_Load);
            this.root.ResumeLayout(false);
            this.root.PerformLayout();
            this.nameRow.ResumeLayout(false);
            this.nameRow.PerformLayout();
            this.eventButtons.ResumeLayout(false);
            this.addGroup.ResumeLayout(false);
            this.newEvent.ResumeLayout(false);
            this.newEvent.PerformLayout();
            this.eventNameRow.ResumeLayout(false);
            this.eventNameRow.PerformLayout();
            this.wavRow.ResumeLayout(false);
            this.wavRow.PerformLayout();
            this.searchRow.ResumeLayout(false);
            this.searchRow.PerformLayout();
            this.addRow.ResumeLayout(false);
            this.addRow.PerformLayout();
            this.buttonRow.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel root;
        private System.Windows.Forms.TableLayoutPanel nameRow;
        private System.Windows.Forms.Label bankNameLabel;
        private System.Windows.Forms.TextBox bankName;
        private System.Windows.Forms.Label eventsLabel;
        private System.Windows.Forms.ListView events;
        private System.Windows.Forms.ColumnHeader eventColumn;
        private System.Windows.Forms.ColumnHeader wavColumn;
        private System.Windows.Forms.ColumnHeader soundsLikeColumn;
        private System.Windows.Forms.ColumnHeader loopsColumn;
        private System.Windows.Forms.FlowLayoutPanel eventButtons;
        private System.Windows.Forms.Button removeEventBtn;
        private System.Windows.Forms.GroupBox addGroup;
        private System.Windows.Forms.TableLayoutPanel newEvent;
        private System.Windows.Forms.TableLayoutPanel eventNameRow;
        private System.Windows.Forms.Label eventNameLabel;
        private System.Windows.Forms.TextBox eventName;
        private System.Windows.Forms.TableLayoutPanel wavRow;
        private System.Windows.Forms.Label wavLabel;
        private System.Windows.Forms.TextBox wavPath;
        private System.Windows.Forms.Button browseBtn;
        private System.Windows.Forms.TableLayoutPanel searchRow;
        private System.Windows.Forms.Label searchLabel;
        private System.Windows.Forms.TextBox search;
        private System.Windows.Forms.ListView sounds;
        private System.Windows.Forms.ColumnHeader soundColumn;
        private System.Windows.Forms.ColumnHeader idColumn;
        private System.Windows.Forms.ColumnHeader keptInColumn;
        private System.Windows.Forms.FlowLayoutPanel addRow;
        private System.Windows.Forms.CheckBox loop;
        private System.Windows.Forms.Button playBtn;
        private System.Windows.Forms.Button stopBtn;
        private System.Windows.Forms.Button addEventBtn;
        private System.Windows.Forms.TextBox summary;
        private System.Windows.Forms.FlowLayoutPanel buttonRow;
        private System.Windows.Forms.Button cancelBtn;
        private System.Windows.Forms.Button saveBtn;
        private System.Windows.Forms.ToolTip tips;
    }
}

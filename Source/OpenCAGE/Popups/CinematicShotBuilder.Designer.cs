namespace OpenCAGE.Popups
{
    partial class CinematicShotBuilder
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
            this.mainSplit = new System.Windows.Forms.SplitContainer();
            this.shotsSplit = new System.Windows.Forms.SplitContainer();
            this.listPanel = new System.Windows.Forms.Panel();
            this._shots = new System.Windows.Forms.ListBox();
            this.listButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.addShotButton = new System.Windows.Forms.Button();
            this.copyShotButton = new System.Windows.Forms.Button();
            this.removeShotButton = new System.Windows.Forms.Button();
            this.upButton = new System.Windows.Forms.Button();
            this.downButton = new System.Windows.Forms.Button();
            this.fromViewportButton = new System.Windows.Forms.Button();
            this._grid = new System.Windows.Forms.PropertyGrid();
            this.resultSplit = new System.Windows.Forms.SplitContainer();
            this._report = new System.Windows.Forms.TextBox();
            this._picture = new System.Windows.Forms.PictureBox();
            this.actionsPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.planButton = new System.Windows.Forms.Button();
            this.buildButton = new System.Windows.Forms.Button();
            this.previewShotButton = new System.Windows.Forms.Button();
            this.readBackButton = new System.Windows.Forms.Button();
            this.openButton = new System.Windows.Forms.Button();
            this.saveAsButton = new System.Windows.Forms.Button();
            this.optionsPanel = new System.Windows.Forms.FlowLayoutPanel();
            this._hideHud = new System.Windows.Forms.CheckBox();
            this._noBlur = new System.Windows.Forms.CheckBox();
            this._dof = new System.Windows.Forms.CheckBox();
            this.cuesLabel = new System.Windows.Forms.Label();
            this._cues = new System.Windows.Forms.TextBox();
            this.clipsLabel = new System.Windows.Forms.Label();
            this._clips = new System.Windows.Forms.TextBox();
            this.topPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.nameLabel = new System.Windows.Forms.Label();
            this._name = new System.Windows.Forms.TextBox();
            this.compositeLabel = new System.Windows.Forms.Label();
            this._composite = new System.Windows.Forms.TextBox();
            this.startsLabel = new System.Windows.Forms.Label();
            this._start = new System.Windows.Forms.ComboBox();
            this.startDelayLabel = new System.Windows.Forms.Label();
            this._startDelay = new System.Windows.Forms.NumericUpDown();
            this.endLabel = new System.Windows.Forms.Label();
            this._end = new System.Windows.Forms.ComboBox();
            this.blendOutLabel = new System.Windows.Forms.Label();
            this._blendOut = new System.Windows.Forms.NumericUpDown();
            this.holdLabel = new System.Windows.Forms.Label();
            this._hold = new System.Windows.Forms.NumericUpDown();
            this._status = new System.Windows.Forms.Label();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.mainSplit)).BeginInit();
            this.mainSplit.Panel1.SuspendLayout();
            this.mainSplit.Panel2.SuspendLayout();
            this.mainSplit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.shotsSplit)).BeginInit();
            this.shotsSplit.Panel1.SuspendLayout();
            this.shotsSplit.Panel2.SuspendLayout();
            this.shotsSplit.SuspendLayout();
            this.listPanel.SuspendLayout();
            this.listButtons.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.resultSplit)).BeginInit();
            this.resultSplit.Panel1.SuspendLayout();
            this.resultSplit.Panel2.SuspendLayout();
            this.resultSplit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._picture)).BeginInit();
            this.actionsPanel.SuspendLayout();
            this.optionsPanel.SuspendLayout();
            this.topPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._startDelay)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._blendOut)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._hold)).BeginInit();
            this.SuspendLayout();
            //
            // mainSplit
            //
            this.mainSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainSplit.Location = new System.Drawing.Point(0, 95);
            this.mainSplit.Name = "mainSplit";
            this.mainSplit.Orientation = System.Windows.Forms.Orientation.Horizontal;
            //
            // mainSplit.Panel1
            //
            this.mainSplit.Panel1.Controls.Add(this.shotsSplit);
            //
            // mainSplit.Panel2
            //
            this.mainSplit.Panel2.Controls.Add(this.resultSplit);
            this.mainSplit.Size = new System.Drawing.Size(1164, 680);
            this.mainSplit.SplitterDistance = 420;
            this.mainSplit.TabIndex = 0;
            //
            // shotsSplit
            //
            this.shotsSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.shotsSplit.Location = new System.Drawing.Point(0, 0);
            this.shotsSplit.Name = "shotsSplit";
            //
            // shotsSplit.Panel1
            //
            this.shotsSplit.Panel1.Controls.Add(this.listPanel);
            //
            // shotsSplit.Panel2
            //
            this.shotsSplit.Panel2.Controls.Add(this._grid);
            this.shotsSplit.Size = new System.Drawing.Size(1164, 420);
            this.shotsSplit.SplitterDistance = 320;
            this.shotsSplit.TabIndex = 0;
            //
            // listPanel
            //
            this.listPanel.Controls.Add(this._shots);
            this.listPanel.Controls.Add(this.listButtons);
            this.listPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listPanel.Location = new System.Drawing.Point(0, 0);
            this.listPanel.Name = "listPanel";
            this.listPanel.Size = new System.Drawing.Size(320, 420);
            this.listPanel.TabIndex = 0;
            //
            // _shots
            //
            this._shots.Dock = System.Windows.Forms.DockStyle.Fill;
            this._shots.IntegralHeight = false;
            this._shots.Location = new System.Drawing.Point(0, 0);
            this._shots.Name = "_shots";
            this._shots.Size = new System.Drawing.Size(320, 356);
            this._shots.TabIndex = 0;
            this._shots.SelectedIndexChanged += new System.EventHandler(this._shots_SelectedIndexChanged);
            //
            // listButtons
            //
            this.listButtons.AutoSize = true;
            this.listButtons.Controls.Add(this.addShotButton);
            this.listButtons.Controls.Add(this.copyShotButton);
            this.listButtons.Controls.Add(this.removeShotButton);
            this.listButtons.Controls.Add(this.upButton);
            this.listButtons.Controls.Add(this.downButton);
            this.listButtons.Controls.Add(this.fromViewportButton);
            this.listButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.listButtons.Location = new System.Drawing.Point(0, 356);
            this.listButtons.Name = "listButtons";
            this.listButtons.Size = new System.Drawing.Size(320, 64);
            this.listButtons.TabIndex = 1;
            //
            // addShotButton
            //
            this.addShotButton.Location = new System.Drawing.Point(3, 3);
            this.addShotButton.Name = "addShotButton";
            this.addShotButton.Size = new System.Drawing.Size(70, 26);
            this.addShotButton.TabIndex = 0;
            this.addShotButton.Text = "Add";
            this.addShotButton.UseVisualStyleBackColor = true;
            this.addShotButton.Click += new System.EventHandler(this.addShotButton_Click);
            //
            // copyShotButton
            //
            this.copyShotButton.Location = new System.Drawing.Point(79, 3);
            this.copyShotButton.Name = "copyShotButton";
            this.copyShotButton.Size = new System.Drawing.Size(70, 26);
            this.copyShotButton.TabIndex = 1;
            this.copyShotButton.Text = "Copy";
            this.copyShotButton.UseVisualStyleBackColor = true;
            this.copyShotButton.Click += new System.EventHandler(this.copyShotButton_Click);
            //
            // removeShotButton
            //
            this.removeShotButton.Location = new System.Drawing.Point(155, 3);
            this.removeShotButton.Name = "removeShotButton";
            this.removeShotButton.Size = new System.Drawing.Size(70, 26);
            this.removeShotButton.TabIndex = 2;
            this.removeShotButton.Text = "Remove";
            this.removeShotButton.UseVisualStyleBackColor = true;
            this.removeShotButton.Click += new System.EventHandler(this.removeShotButton_Click);
            //
            // upButton
            //
            this.upButton.Location = new System.Drawing.Point(231, 3);
            this.upButton.Name = "upButton";
            this.upButton.Size = new System.Drawing.Size(70, 26);
            this.upButton.TabIndex = 3;
            this.upButton.Text = "Up";
            this.upButton.UseVisualStyleBackColor = true;
            this.upButton.Click += new System.EventHandler(this.upButton_Click);
            //
            // downButton
            //
            this.downButton.Location = new System.Drawing.Point(3, 35);
            this.downButton.Name = "downButton";
            this.downButton.Size = new System.Drawing.Size(70, 26);
            this.downButton.TabIndex = 4;
            this.downButton.Text = "Down";
            this.downButton.UseVisualStyleBackColor = true;
            this.downButton.Click += new System.EventHandler(this.downButton_Click);
            //
            // fromViewportButton
            //
            this.fromViewportButton.Location = new System.Drawing.Point(79, 35);
            this.fromViewportButton.Name = "fromViewportButton";
            this.fromViewportButton.Size = new System.Drawing.Size(110, 26);
            this.fromViewportButton.TabIndex = 5;
            this.fromViewportButton.Text = "From Viewport";
            this.fromViewportButton.UseVisualStyleBackColor = true;
            this.fromViewportButton.Click += new System.EventHandler(this.fromViewportButton_Click);
            //
            // _grid
            //
            this._grid.Dock = System.Windows.Forms.DockStyle.Fill;
            this._grid.Location = new System.Drawing.Point(0, 0);
            this._grid.Name = "_grid";
            this._grid.PropertySort = System.Windows.Forms.PropertySort.Categorized;
            this._grid.Size = new System.Drawing.Size(840, 420);
            this._grid.TabIndex = 0;
            this._grid.ToolbarVisible = false;
            this._grid.PropertyValueChanged += new System.Windows.Forms.PropertyValueChangedEventHandler(this._grid_PropertyValueChanged);
            //
            // resultSplit
            //
            this.resultSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.resultSplit.Location = new System.Drawing.Point(0, 0);
            this.resultSplit.Name = "resultSplit";
            //
            // resultSplit.Panel1
            //
            this.resultSplit.Panel1.Controls.Add(this._report);
            //
            // resultSplit.Panel2
            //
            this.resultSplit.Panel2.Controls.Add(this._picture);
            this.resultSplit.Size = new System.Drawing.Size(1164, 256);
            this.resultSplit.SplitterDistance = 704;
            this.resultSplit.TabIndex = 0;
            //
            // _report
            //
            this._report.Dock = System.Windows.Forms.DockStyle.Fill;
            this._report.Font = new System.Drawing.Font("Courier New", 8.25F);
            this._report.Location = new System.Drawing.Point(0, 0);
            this._report.Multiline = true;
            this._report.Name = "_report";
            this._report.ReadOnly = true;
            this._report.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this._report.Size = new System.Drawing.Size(704, 256);
            this._report.TabIndex = 0;
            this._report.WordWrap = false;
            //
            // _picture
            //
            this._picture.BackColor = System.Drawing.Color.Black;
            this._picture.Dock = System.Windows.Forms.DockStyle.Fill;
            this._picture.Location = new System.Drawing.Point(0, 0);
            this._picture.Name = "_picture";
            this._picture.Size = new System.Drawing.Size(456, 256);
            this._picture.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this._picture.TabIndex = 0;
            this._picture.TabStop = false;
            //
            // actionsPanel
            //
            this.actionsPanel.AutoSize = true;
            this.actionsPanel.Controls.Add(this.planButton);
            this.actionsPanel.Controls.Add(this.buildButton);
            this.actionsPanel.Controls.Add(this.previewShotButton);
            this.actionsPanel.Controls.Add(this.readBackButton);
            this.actionsPanel.Controls.Add(this.openButton);
            this.actionsPanel.Controls.Add(this.saveAsButton);
            this.actionsPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.actionsPanel.Location = new System.Drawing.Point(0, 775);
            this.actionsPanel.Name = "actionsPanel";
            this.actionsPanel.Padding = new System.Windows.Forms.Padding(6);
            this.actionsPanel.Size = new System.Drawing.Size(1164, 44);
            this.actionsPanel.TabIndex = 1;
            //
            // planButton
            //
            this.planButton.Location = new System.Drawing.Point(9, 9);
            this.planButton.Name = "planButton";
            this.planButton.Size = new System.Drawing.Size(90, 26);
            this.planButton.TabIndex = 0;
            this.planButton.Text = "Plan";
            this.planButton.UseVisualStyleBackColor = true;
            this.planButton.Click += new System.EventHandler(this.planButton_Click);
            //
            // buildButton
            //
            this.buildButton.Location = new System.Drawing.Point(105, 9);
            this.buildButton.Name = "buildButton";
            this.buildButton.Size = new System.Drawing.Size(90, 26);
            this.buildButton.TabIndex = 1;
            this.buildButton.Text = "Build";
            this.buildButton.UseVisualStyleBackColor = true;
            this.buildButton.Click += new System.EventHandler(this.buildButton_Click);
            //
            // previewShotButton
            //
            this.previewShotButton.Location = new System.Drawing.Point(201, 9);
            this.previewShotButton.Name = "previewShotButton";
            this.previewShotButton.Size = new System.Drawing.Size(110, 26);
            this.previewShotButton.TabIndex = 2;
            this.previewShotButton.Text = "Preview Shot";
            this.previewShotButton.UseVisualStyleBackColor = true;
            this.previewShotButton.Click += new System.EventHandler(this.previewShotButton_Click);
            //
            // readBackButton
            //
            this.readBackButton.Location = new System.Drawing.Point(317, 9);
            this.readBackButton.Name = "readBackButton";
            this.readBackButton.Size = new System.Drawing.Size(100, 26);
            this.readBackButton.TabIndex = 3;
            this.readBackButton.Text = "Read Back";
            this.readBackButton.UseVisualStyleBackColor = true;
            this.readBackButton.Click += new System.EventHandler(this.readBackButton_Click);
            //
            // openButton
            //
            this.openButton.Location = new System.Drawing.Point(423, 9);
            this.openButton.Name = "openButton";
            this.openButton.Size = new System.Drawing.Size(80, 26);
            this.openButton.TabIndex = 4;
            this.openButton.Text = "Open...";
            this.openButton.UseVisualStyleBackColor = true;
            this.openButton.Click += new System.EventHandler(this.openButton_Click);
            //
            // saveAsButton
            //
            this.saveAsButton.Location = new System.Drawing.Point(509, 9);
            this.saveAsButton.Name = "saveAsButton";
            this.saveAsButton.Size = new System.Drawing.Size(90, 26);
            this.saveAsButton.TabIndex = 5;
            this.saveAsButton.Text = "Save As...";
            this.saveAsButton.UseVisualStyleBackColor = true;
            this.saveAsButton.Click += new System.EventHandler(this.saveAsButton_Click);
            //
            // optionsPanel
            //
            this.optionsPanel.AutoSize = true;
            this.optionsPanel.Controls.Add(this._hideHud);
            this.optionsPanel.Controls.Add(this._noBlur);
            this.optionsPanel.Controls.Add(this._dof);
            this.optionsPanel.Controls.Add(this.cuesLabel);
            this.optionsPanel.Controls.Add(this._cues);
            this.optionsPanel.Controls.Add(this.clipsLabel);
            this.optionsPanel.Controls.Add(this._clips);
            this.optionsPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.optionsPanel.Location = new System.Drawing.Point(0, 65);
            this.optionsPanel.Name = "optionsPanel";
            this.optionsPanel.Padding = new System.Windows.Forms.Padding(6, 0, 6, 4);
            this.optionsPanel.Size = new System.Drawing.Size(1164, 30);
            this.optionsPanel.TabIndex = 2;
            //
            // _hideHud
            //
            this._hideHud.AutoSize = true;
            this._hideHud.Checked = true;
            this._hideHud.CheckState = System.Windows.Forms.CheckState.Checked;
            this._hideHud.Location = new System.Drawing.Point(9, 3);
            this._hideHud.Name = "_hideHud";
            this._hideHud.Size = new System.Drawing.Size(75, 17);
            this._hideHud.TabIndex = 0;
            this._hideHud.Text = "Hide HUD";
            //
            // _noBlur
            //
            this._noBlur.AutoSize = true;
            this._noBlur.Checked = true;
            this._noBlur.CheckState = System.Windows.Forms.CheckState.Checked;
            this._noBlur.Location = new System.Drawing.Point(90, 3);
            this._noBlur.Name = "_noBlur";
            this._noBlur.Size = new System.Drawing.Size(93, 17);
            this._noBlur.TabIndex = 1;
            this._noBlur.Text = "Motion blur off";
            //
            // _dof
            //
            this._dof.AutoSize = true;
            this._dof.Checked = true;
            this._dof.CheckState = System.Windows.Forms.CheckState.Checked;
            this._dof.Location = new System.Drawing.Point(189, 3);
            this._dof.Name = "_dof";
            this._dof.Size = new System.Drawing.Size(89, 17);
            this._dof.TabIndex = 2;
            this._dof.Text = "Depth of field";
            //
            // cuesLabel
            //
            this.cuesLabel.AutoSize = true;
            this.cuesLabel.Location = new System.Drawing.Point(284, 0);
            this.cuesLabel.Name = "cuesLabel";
            this.cuesLabel.Padding = new System.Windows.Forms.Padding(4, 6, 0, 0);
            this.cuesLabel.Size = new System.Drawing.Size(35, 19);
            this.cuesLabel.TabIndex = 3;
            this.cuesLabel.Text = "Cues";
            //
            // _cues
            //
            this._cues.Location = new System.Drawing.Point(325, 3);
            this._cues.Name = "_cues";
            this._cues.Size = new System.Drawing.Size(300, 20);
            this._cues.TabIndex = 4;
            this.toolTip.SetToolTip(this._cues, "Things to call at times, as JSON: [{\"time\": 2.5, \"call\": \"Door_1.open\"}]");
            //
            // clipsLabel
            //
            this.clipsLabel.AutoSize = true;
            this.clipsLabel.Location = new System.Drawing.Point(631, 0);
            this.clipsLabel.Name = "clipsLabel";
            this.clipsLabel.Padding = new System.Windows.Forms.Padding(4, 6, 0, 0);
            this.clipsLabel.Size = new System.Drawing.Size(53, 19);
            this.clipsLabel.TabIndex = 5;
            this.clipsLabel.Text = "Clip sync";
            //
            // _clips
            //
            this._clips.Location = new System.Drawing.Point(690, 3);
            this._clips.Name = "_clips";
            this._clips.Size = new System.Drawing.Size(300, 20);
            this._clips.TabIndex = 6;
            this.toolTip.SetToolTip(this._clips, "Character clips locked to the timeline, as JSON: [{\"entity\": \"Anim_1\", \"at\": 1.0, \"start\": \"Bind_1.trigger\"}] (a CMD_PlayAnimation; clip time 0 at \'at\').");
            //
            // topPanel
            //
            this.topPanel.AutoSize = true;
            this.topPanel.Controls.Add(this.nameLabel);
            this.topPanel.Controls.Add(this._name);
            this.topPanel.Controls.Add(this.compositeLabel);
            this.topPanel.Controls.Add(this._composite);
            this.topPanel.Controls.Add(this.startsLabel);
            this.topPanel.Controls.Add(this._start);
            this.topPanel.Controls.Add(this.startDelayLabel);
            this.topPanel.Controls.Add(this._startDelay);
            this.topPanel.Controls.Add(this.endLabel);
            this.topPanel.Controls.Add(this._end);
            this.topPanel.Controls.Add(this.blendOutLabel);
            this.topPanel.Controls.Add(this._blendOut);
            this.topPanel.Controls.Add(this.holdLabel);
            this.topPanel.Controls.Add(this._hold);
            this.topPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.topPanel.Location = new System.Drawing.Point(0, 0);
            this.topPanel.Name = "topPanel";
            this.topPanel.Padding = new System.Windows.Forms.Padding(6);
            this.topPanel.Size = new System.Drawing.Size(1164, 65);
            this.topPanel.TabIndex = 3;
            //
            // nameLabel
            //
            this.nameLabel.AutoSize = true;
            this.nameLabel.Location = new System.Drawing.Point(9, 6);
            this.nameLabel.Name = "nameLabel";
            this.nameLabel.Padding = new System.Windows.Forms.Padding(4, 6, 0, 0);
            this.nameLabel.Size = new System.Drawing.Size(39, 19);
            this.nameLabel.TabIndex = 0;
            this.nameLabel.Text = "Name";
            //
            // _name
            //
            this._name.Location = new System.Drawing.Point(54, 9);
            this._name.Name = "_name";
            this._name.Size = new System.Drawing.Size(160, 20);
            this._name.TabIndex = 1;
            this._name.Text = "Cinematic";
            //
            // compositeLabel
            //
            this.compositeLabel.AutoSize = true;
            this.compositeLabel.Location = new System.Drawing.Point(220, 6);
            this.compositeLabel.Name = "compositeLabel";
            this.compositeLabel.Padding = new System.Windows.Forms.Padding(4, 6, 0, 0);
            this.compositeLabel.Size = new System.Drawing.Size(71, 19);
            this.compositeLabel.TabIndex = 2;
            this.compositeLabel.Text = "In composite";
            //
            // _composite
            //
            this._composite.Location = new System.Drawing.Point(297, 9);
            this._composite.Name = "_composite";
            this._composite.Size = new System.Drawing.Size(260, 20);
            this._composite.TabIndex = 3;
            this._composite.Text = "root";
            //
            // startsLabel
            //
            this.startsLabel.AutoSize = true;
            this.startsLabel.Location = new System.Drawing.Point(563, 6);
            this.startsLabel.Name = "startsLabel";
            this.startsLabel.Padding = new System.Windows.Forms.Padding(4, 6, 0, 0);
            this.startsLabel.Size = new System.Drawing.Size(38, 19);
            this.startsLabel.TabIndex = 4;
            this.startsLabel.Text = "Starts";
            //
            // _start
            //
            this._start.Items.AddRange(new object[] {
            "level_start",
            "none"});
            this._start.Location = new System.Drawing.Point(607, 9);
            this._start.Name = "_start";
            this._start.Size = new System.Drawing.Size(230, 21);
            this._start.TabIndex = 5;
            this._start.Text = "level_start";
            this.toolTip.SetToolTip(this._start, "level_start: after the level has loaded (and the delay). none: link something to <name>_Begin.trigger yourself. Or type \'Entity.pin\', e.g. PlayerTriggerBox_1.on_entered.");
            //
            // startDelayLabel
            //
            this.startDelayLabel.AutoSize = true;
            this.startDelayLabel.Location = new System.Drawing.Point(843, 6);
            this.startDelayLabel.Name = "startDelayLabel";
            this.startDelayLabel.Padding = new System.Windows.Forms.Padding(4, 6, 0, 0);
            this.startDelayLabel.Size = new System.Drawing.Size(46, 19);
            this.startDelayLabel.TabIndex = 6;
            this.startDelayLabel.Text = "after (s)";
            //
            // _startDelay
            //
            this._startDelay.DecimalPlaces = 2;
            this._startDelay.Increment = new decimal(new int[] {
            25,
            0,
            0,
            131072});
            this._startDelay.Location = new System.Drawing.Point(895, 9);
            this._startDelay.Maximum = new decimal(new int[] {
            600,
            0,
            0,
            0});
            this._startDelay.Name = "_startDelay";
            this._startDelay.Size = new System.Drawing.Size(60, 20);
            this._startDelay.TabIndex = 7;
            this._startDelay.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            //
            // endLabel
            //
            this.endLabel.AutoSize = true;
            this.endLabel.Location = new System.Drawing.Point(961, 6);
            this.endLabel.Name = "endLabel";
            this.endLabel.Padding = new System.Windows.Forms.Padding(4, 6, 0, 0);
            this.endLabel.Size = new System.Drawing.Size(30, 19);
            this.endLabel.TabIndex = 8;
            this.endLabel.Text = "End";
            //
            // _end
            //
            this._end.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._end.Items.AddRange(new object[] {
            "return",
            "hold",
            "loop"});
            this._end.Location = new System.Drawing.Point(997, 9);
            this._end.Name = "_end";
            this._end.Size = new System.Drawing.Size(80, 21);
            this._end.TabIndex = 9;
            //
            // blendOutLabel
            //
            this.blendOutLabel.AutoSize = true;
            this.blendOutLabel.Location = new System.Drawing.Point(1083, 6);
            this.blendOutLabel.Name = "blendOutLabel";
            this.blendOutLabel.Padding = new System.Windows.Forms.Padding(4, 6, 0, 0);
            this.blendOutLabel.Size = new System.Drawing.Size(69, 19);
            this.blendOutLabel.TabIndex = 10;
            this.blendOutLabel.Text = "blend out (s)";
            //
            // _blendOut
            //
            this._blendOut.DecimalPlaces = 2;
            this._blendOut.Increment = new decimal(new int[] {
            25,
            0,
            0,
            131072});
            this._blendOut.Location = new System.Drawing.Point(9, 36);
            this._blendOut.Maximum = new decimal(new int[] {
            60,
            0,
            0,
            0});
            this._blendOut.Name = "_blendOut";
            this._blendOut.Size = new System.Drawing.Size(60, 20);
            this._blendOut.TabIndex = 11;
            //
            // holdLabel
            //
            this.holdLabel.AutoSize = true;
            this.holdLabel.Location = new System.Drawing.Point(75, 33);
            this.holdLabel.Name = "holdLabel";
            this.holdLabel.Padding = new System.Windows.Forms.Padding(4, 6, 0, 0);
            this.holdLabel.Size = new System.Drawing.Size(45, 19);
            this.holdLabel.TabIndex = 12;
            this.holdLabel.Text = "hold (s)";
            //
            // _hold
            //
            this._hold.DecimalPlaces = 2;
            this._hold.Increment = new decimal(new int[] {
            25,
            0,
            0,
            131072});
            this._hold.Location = new System.Drawing.Point(126, 36);
            this._hold.Maximum = new decimal(new int[] {
            600,
            0,
            0,
            0});
            this._hold.Name = "_hold";
            this._hold.Size = new System.Drawing.Size(60, 20);
            this._hold.TabIndex = 13;
            //
            // _status
            //
            this._status.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._status.Location = new System.Drawing.Point(0, 819);
            this._status.Name = "_status";
            this._status.Padding = new System.Windows.Forms.Padding(6, 4, 0, 0);
            this._status.Size = new System.Drawing.Size(1164, 22);
            this._status.TabIndex = 4;
            //
            // CinematicShotBuilder
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1164, 841);
            this.Controls.Add(this.mainSplit);
            this.Controls.Add(this.actionsPanel);
            this.Controls.Add(this.optionsPanel);
            this.Controls.Add(this.topPanel);
            this.Controls.Add(this._status);
            this.MinimumSize = new System.Drawing.Size(900, 640);
            this.Name = "CinematicShotBuilder";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Cinematic Shot Builder";
            this.Load += new System.EventHandler(this.CinematicShotBuilder_Load);
            this.mainSplit.Panel1.ResumeLayout(false);
            this.mainSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.mainSplit)).EndInit();
            this.mainSplit.ResumeLayout(false);
            this.shotsSplit.Panel1.ResumeLayout(false);
            this.shotsSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.shotsSplit)).EndInit();
            this.shotsSplit.ResumeLayout(false);
            this.listPanel.ResumeLayout(false);
            this.listPanel.PerformLayout();
            this.listButtons.ResumeLayout(false);
            this.resultSplit.Panel1.ResumeLayout(false);
            this.resultSplit.Panel1.PerformLayout();
            this.resultSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.resultSplit)).EndInit();
            this.resultSplit.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._picture)).EndInit();
            this.actionsPanel.ResumeLayout(false);
            this.optionsPanel.ResumeLayout(false);
            this.optionsPanel.PerformLayout();
            this.topPanel.ResumeLayout(false);
            this.topPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._startDelay)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._blendOut)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._hold)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer mainSplit;
        private System.Windows.Forms.SplitContainer shotsSplit;
        private System.Windows.Forms.Panel listPanel;
        private System.Windows.Forms.ListBox _shots;
        private System.Windows.Forms.FlowLayoutPanel listButtons;
        private System.Windows.Forms.Button addShotButton;
        private System.Windows.Forms.Button copyShotButton;
        private System.Windows.Forms.Button removeShotButton;
        private System.Windows.Forms.Button upButton;
        private System.Windows.Forms.Button downButton;
        private System.Windows.Forms.Button fromViewportButton;
        private System.Windows.Forms.PropertyGrid _grid;
        private System.Windows.Forms.SplitContainer resultSplit;
        private System.Windows.Forms.TextBox _report;
        private System.Windows.Forms.PictureBox _picture;
        private System.Windows.Forms.FlowLayoutPanel actionsPanel;
        private System.Windows.Forms.Button planButton;
        private System.Windows.Forms.Button buildButton;
        private System.Windows.Forms.Button previewShotButton;
        private System.Windows.Forms.Button readBackButton;
        private System.Windows.Forms.Button openButton;
        private System.Windows.Forms.Button saveAsButton;
        private System.Windows.Forms.FlowLayoutPanel optionsPanel;
        private System.Windows.Forms.CheckBox _hideHud;
        private System.Windows.Forms.CheckBox _noBlur;
        private System.Windows.Forms.CheckBox _dof;
        private System.Windows.Forms.Label cuesLabel;
        private System.Windows.Forms.TextBox _cues;
        private System.Windows.Forms.Label clipsLabel;
        private System.Windows.Forms.TextBox _clips;
        private System.Windows.Forms.FlowLayoutPanel topPanel;
        private System.Windows.Forms.Label nameLabel;
        private System.Windows.Forms.TextBox _name;
        private System.Windows.Forms.Label compositeLabel;
        private System.Windows.Forms.TextBox _composite;
        private System.Windows.Forms.Label startsLabel;
        private System.Windows.Forms.ComboBox _start;
        private System.Windows.Forms.Label startDelayLabel;
        private System.Windows.Forms.NumericUpDown _startDelay;
        private System.Windows.Forms.Label endLabel;
        private System.Windows.Forms.ComboBox _end;
        private System.Windows.Forms.Label blendOutLabel;
        private System.Windows.Forms.NumericUpDown _blendOut;
        private System.Windows.Forms.Label holdLabel;
        private System.Windows.Forms.NumericUpDown _hold;
        private System.Windows.Forms.Label _status;
        private System.Windows.Forms.ToolTip toolTip;
    }
}

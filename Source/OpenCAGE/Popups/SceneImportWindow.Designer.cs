namespace OpenCAGE.Popups
{
    partial class SceneImportWindow
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
            this._split = new System.Windows.Forms.SplitContainer();
            this.optionsPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.noteLabel = new System.Windows.Forms.Label();
            this.fileRow = new System.Windows.Forms.FlowLayoutPanel();
            this.fileLabel = new System.Windows.Forms.Label();
            this._file = new System.Windows.Forms.TextBox();
            this._browse = new System.Windows.Forms.Button();
            this.nameRow = new System.Windows.Forms.FlowLayoutPanel();
            this.nameLabel = new System.Windows.Forms.Label();
            this._name = new System.Windows.Forms.TextBox();
            this.scaleLabel = new System.Windows.Forms.Label();
            this._scale = new System.Windows.Forms.NumericUpDown();
            this.placeRow = new System.Windows.Forms.FlowLayoutPanel();
            this.placeLabel = new System.Windows.Forms.Label();
            this._px = new System.Windows.Forms.NumericUpDown();
            this._py = new System.Windows.Forms.NumericUpDown();
            this._pz = new System.Windows.Forms.NumericUpDown();
            this.turnLabel = new System.Windows.Forms.Label();
            this._yaw = new System.Windows.Forms.NumericUpDown();
            this.groupRow = new System.Windows.Forms.FlowLayoutPanel();
            this.groupLabel = new System.Windows.Forms.Label();
            this._group = new System.Windows.Forms.ComboBox();
            this.collisionRow = new System.Windows.Forms.FlowLayoutPanel();
            this.collisionLabel = new System.Windows.Forms.Label();
            this._collision = new System.Windows.Forms.ComboBox();
            this.physicsRow = new System.Windows.Forms.FlowLayoutPanel();
            this.physicsLabel = new System.Windows.Forms.Label();
            this._physics = new System.Windows.Forms.TextBox();
            this.physicsHint = new System.Windows.Forms.Label();
            this.excludeRow = new System.Windows.Forms.FlowLayoutPanel();
            this.excludeLabel = new System.Windows.Forms.Label();
            this._exclude = new System.Windows.Forms.TextBox();
            this.excludeHint = new System.Windows.Forms.Label();
            this.lightsRow = new System.Windows.Forms.FlowLayoutPanel();
            this.lightsLabel = new System.Windows.Forms.Label();
            this._lights = new System.Windows.Forms.CheckBox();
            this.brightnessLabel = new System.Windows.Forms.Label();
            this._lightScale = new System.Windows.Forms.NumericUpDown();
            this._shadows = new System.Windows.Forms.ComboBox();
            this.sunRow = new System.Windows.Forms.FlowLayoutPanel();
            this.sunSpacer = new System.Windows.Forms.Label();
            this._sun = new System.Windows.Forms.CheckBox();
            this.mergeRow = new System.Windows.Forms.FlowLayoutPanel();
            this.mergeSpacer = new System.Windows.Forms.Label();
            this._merge = new System.Windows.Forms.CheckBox();
            this.compactRow = new System.Windows.Forms.FlowLayoutPanel();
            this.compactSpacer = new System.Windows.Forms.Label();
            this._compact = new System.Windows.Forms.CheckBox();
            this._report = new System.Windows.Forms.TextBox();
            this.buttonsPanel = new System.Windows.Forms.FlowLayoutPanel();
            this._dryRun = new System.Windows.Forms.Button();
            this._import = new System.Windows.Forms.Button();
            this._status = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this._split)).BeginInit();
            this._split.Panel1.SuspendLayout();
            this._split.Panel2.SuspendLayout();
            this._split.SuspendLayout();
            this.optionsPanel.SuspendLayout();
            this.fileRow.SuspendLayout();
            this.nameRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._scale)).BeginInit();
            this.placeRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._px)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._py)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._pz)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._yaw)).BeginInit();
            this.groupRow.SuspendLayout();
            this.collisionRow.SuspendLayout();
            this.physicsRow.SuspendLayout();
            this.excludeRow.SuspendLayout();
            this.lightsRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._lightScale)).BeginInit();
            this.sunRow.SuspendLayout();
            this.mergeRow.SuspendLayout();
            this.compactRow.SuspendLayout();
            this.buttonsPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // _split
            //
            this._split.Dock = System.Windows.Forms.DockStyle.Fill;
            this._split.Location = new System.Drawing.Point(0, 0);
            this._split.Name = "_split";
            this._split.Orientation = System.Windows.Forms.Orientation.Horizontal;
            //
            // _split.Panel1
            //
            this._split.Panel1.Controls.Add(this.optionsPanel);
            //
            // _split.Panel2
            //
            this._split.Panel2.Controls.Add(this._report);
            this._split.Size = new System.Drawing.Size(924, 767);
            this._split.SplitterDistance = 426;
            this._split.TabIndex = 0;
            //
            // optionsPanel
            //
            this.optionsPanel.AutoScroll = true;
            this.optionsPanel.Controls.Add(this.noteLabel);
            this.optionsPanel.Controls.Add(this.fileRow);
            this.optionsPanel.Controls.Add(this.nameRow);
            this.optionsPanel.Controls.Add(this.placeRow);
            this.optionsPanel.Controls.Add(this.groupRow);
            this.optionsPanel.Controls.Add(this.collisionRow);
            this.optionsPanel.Controls.Add(this.physicsRow);
            this.optionsPanel.Controls.Add(this.excludeRow);
            this.optionsPanel.Controls.Add(this.lightsRow);
            this.optionsPanel.Controls.Add(this.sunRow);
            this.optionsPanel.Controls.Add(this.mergeRow);
            this.optionsPanel.Controls.Add(this.compactRow);
            this.optionsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.optionsPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.optionsPanel.Location = new System.Drawing.Point(0, 0);
            this.optionsPanel.Name = "optionsPanel";
            this.optionsPanel.Padding = new System.Windows.Forms.Padding(6);
            this.optionsPanel.Size = new System.Drawing.Size(924, 426);
            this.optionsPanel.TabIndex = 0;
            this.optionsPanel.WrapContents = false;
            //
            // noteLabel
            //
            this.noteLabel.AutoSize = true;
            this.noteLabel.ForeColor = System.Drawing.SystemColors.GrayText;
            this.noteLabel.Location = new System.Drawing.Point(9, 6);
            this.noteLabel.MaximumSize = new System.Drawing.Size(880, 0);
            this.noteLabel.Name = "noteLabel";
            this.noteLabel.Padding = new System.Windows.Forms.Padding(3, 0, 0, 4);
            this.noteLabel.Size = new System.Drawing.Size(880, 43);
            this.noteLabel.TabIndex = 0;
            this.noteLabel.Text = "A whole scene file - glTF / GLB, FBX, COLLADA or OBJ - into the open level: every node with meshes is placed, and nodes drawing the same meshes at the same scale share one model. Each model gets generated materials, a composite and collision made to match it; nodes named like a physics pattern become physics props; the file\'s lights come in too. Dry Run lists everything with the budgets it runs into and changes nothing; Import is one undo step.";
            //
            // fileRow
            //
            this.fileRow.AutoSize = true;
            this.fileRow.Controls.Add(this.fileLabel);
            this.fileRow.Controls.Add(this._file);
            this.fileRow.Controls.Add(this._browse);
            this.fileRow.Location = new System.Drawing.Point(6, 51);
            this.fileRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.fileRow.Name = "fileRow";
            this.fileRow.Size = new System.Drawing.Size(623, 29);
            this.fileRow.TabIndex = 1;
            this.fileRow.WrapContents = false;
            //
            // fileLabel
            //
            this.fileLabel.Location = new System.Drawing.Point(3, 3);
            this.fileLabel.Margin = new System.Windows.Forms.Padding(3);
            this.fileLabel.Name = "fileLabel";
            this.fileLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.fileLabel.Size = new System.Drawing.Size(110, 23);
            this.fileLabel.TabIndex = 0;
            this.fileLabel.Text = "File:";
            //
            // _file
            //
            this._file.Location = new System.Drawing.Point(119, 3);
            this._file.Name = "_file";
            this._file.Size = new System.Drawing.Size(420, 20);
            this._file.TabIndex = 1;
            this._file.TextChanged += new System.EventHandler(this._file_TextChanged);
            //
            // _browse
            //
            this._browse.AutoSize = true;
            this._browse.Location = new System.Drawing.Point(545, 3);
            this._browse.Name = "_browse";
            this._browse.Size = new System.Drawing.Size(75, 23);
            this._browse.TabIndex = 2;
            this._browse.Text = "Browse...";
            this._browse.Click += new System.EventHandler(this._browse_Click);
            //
            // nameRow
            //
            this.nameRow.AutoSize = true;
            this.nameRow.Controls.Add(this.nameLabel);
            this.nameRow.Controls.Add(this._name);
            this.nameRow.Controls.Add(this.scaleLabel);
            this.nameRow.Controls.Add(this._scale);
            this.nameRow.Location = new System.Drawing.Point(6, 84);
            this.nameRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.nameRow.Name = "nameRow";
            this.nameRow.Size = new System.Drawing.Size(451, 29);
            this.nameRow.TabIndex = 2;
            this.nameRow.WrapContents = false;
            //
            // nameLabel
            //
            this.nameLabel.Location = new System.Drawing.Point(3, 3);
            this.nameLabel.Margin = new System.Windows.Forms.Padding(3);
            this.nameLabel.Name = "nameLabel";
            this.nameLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.nameLabel.Size = new System.Drawing.Size(110, 23);
            this.nameLabel.TabIndex = 0;
            this.nameLabel.Text = "Name:";
            //
            // _name
            //
            this._name.Location = new System.Drawing.Point(119, 3);
            this._name.Name = "_name";
            this._name.Size = new System.Drawing.Size(200, 20);
            this._name.TabIndex = 1;
            this._name.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this._name_KeyPress);
            //
            // scaleLabel
            //
            this.scaleLabel.AutoSize = true;
            this.scaleLabel.Location = new System.Drawing.Point(325, 3);
            this.scaleLabel.Margin = new System.Windows.Forms.Padding(3);
            this.scaleLabel.Name = "scaleLabel";
            this.scaleLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.scaleLabel.Size = new System.Drawing.Size(37, 17);
            this.scaleLabel.TabIndex = 2;
            this.scaleLabel.Text = "Scale:";
            //
            // _scale
            //
            this._scale.DecimalPlaces = 4;
            this._scale.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this._scale.Location = new System.Drawing.Point(368, 3);
            this._scale.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this._scale.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            262144});
            this._scale.Name = "_scale";
            this._scale.Size = new System.Drawing.Size(80, 20);
            this._scale.TabIndex = 3;
            this._scale.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            //
            // placeRow
            //
            this.placeRow.AutoSize = true;
            this.placeRow.Controls.Add(this.placeLabel);
            this.placeRow.Controls.Add(this._px);
            this.placeRow.Controls.Add(this._py);
            this.placeRow.Controls.Add(this._pz);
            this.placeRow.Controls.Add(this.turnLabel);
            this.placeRow.Controls.Add(this._yaw);
            this.placeRow.Location = new System.Drawing.Point(6, 117);
            this.placeRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.placeRow.Name = "placeRow";
            this.placeRow.Size = new System.Drawing.Size(525, 29);
            this.placeRow.TabIndex = 3;
            this.placeRow.WrapContents = false;
            //
            // placeLabel
            //
            this.placeLabel.Location = new System.Drawing.Point(3, 3);
            this.placeLabel.Margin = new System.Windows.Forms.Padding(3);
            this.placeLabel.Name = "placeLabel";
            this.placeLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.placeLabel.Size = new System.Drawing.Size(110, 23);
            this.placeLabel.TabIndex = 0;
            this.placeLabel.Text = "Place at:";
            //
            // _px
            //
            this._px.DecimalPlaces = 2;
            this._px.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this._px.Location = new System.Drawing.Point(119, 3);
            this._px.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this._px.Minimum = new decimal(new int[] {
            100000,
            0,
            0,
            -2147483648});
            this._px.Name = "_px";
            this._px.Size = new System.Drawing.Size(80, 20);
            this._px.TabIndex = 1;
            //
            // _py
            //
            this._py.DecimalPlaces = 2;
            this._py.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this._py.Location = new System.Drawing.Point(205, 3);
            this._py.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this._py.Minimum = new decimal(new int[] {
            100000,
            0,
            0,
            -2147483648});
            this._py.Name = "_py";
            this._py.Size = new System.Drawing.Size(80, 20);
            this._py.TabIndex = 2;
            //
            // _pz
            //
            this._pz.DecimalPlaces = 2;
            this._pz.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this._pz.Location = new System.Drawing.Point(291, 3);
            this._pz.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this._pz.Minimum = new decimal(new int[] {
            100000,
            0,
            0,
            -2147483648});
            this._pz.Name = "_pz";
            this._pz.Size = new System.Drawing.Size(80, 20);
            this._pz.TabIndex = 3;
            //
            // turnLabel
            //
            this.turnLabel.AutoSize = true;
            this.turnLabel.Location = new System.Drawing.Point(377, 3);
            this.turnLabel.Margin = new System.Windows.Forms.Padding(3);
            this.turnLabel.Name = "turnLabel";
            this.turnLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.turnLabel.Size = new System.Drawing.Size(59, 17);
            this.turnLabel.TabIndex = 4;
            this.turnLabel.Text = "Turn (deg):";
            //
            // _yaw
            //
            this._yaw.DecimalPlaces = 1;
            this._yaw.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this._yaw.Location = new System.Drawing.Point(442, 3);
            this._yaw.Maximum = new decimal(new int[] {
            360,
            0,
            0,
            0});
            this._yaw.Minimum = new decimal(new int[] {
            360,
            0,
            0,
            -2147483648});
            this._yaw.Name = "_yaw";
            this._yaw.Size = new System.Drawing.Size(80, 20);
            this._yaw.TabIndex = 5;
            //
            // groupRow
            //
            this.groupRow.AutoSize = true;
            this.groupRow.Controls.Add(this.groupLabel);
            this.groupRow.Controls.Add(this._group);
            this.groupRow.Location = new System.Drawing.Point(6, 150);
            this.groupRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.groupRow.Name = "groupRow";
            this.groupRow.Size = new System.Drawing.Size(422, 29);
            this.groupRow.TabIndex = 4;
            this.groupRow.WrapContents = false;
            //
            // groupLabel
            //
            this.groupLabel.Location = new System.Drawing.Point(3, 3);
            this.groupLabel.Margin = new System.Windows.Forms.Padding(3);
            this.groupLabel.Name = "groupLabel";
            this.groupLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.groupLabel.Size = new System.Drawing.Size(110, 23);
            this.groupLabel.TabIndex = 0;
            this.groupLabel.Text = "Group:";
            //
            // _group
            //
            this._group.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._group.Items.AddRange(new object[] {
            "A composite per top-level node of the file",
            "One composite for the whole scene"});
            this._group.Location = new System.Drawing.Point(119, 3);
            this._group.Name = "_group";
            this._group.Size = new System.Drawing.Size(300, 21);
            this._group.TabIndex = 1;
            //
            // collisionRow
            //
            this.collisionRow.AutoSize = true;
            this.collisionRow.Controls.Add(this.collisionLabel);
            this.collisionRow.Controls.Add(this._collision);
            this.collisionRow.Location = new System.Drawing.Point(6, 183);
            this.collisionRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.collisionRow.Name = "collisionRow";
            this.collisionRow.Size = new System.Drawing.Size(422, 29);
            this.collisionRow.TabIndex = 5;
            this.collisionRow.WrapContents = false;
            //
            // collisionLabel
            //
            this.collisionLabel.Location = new System.Drawing.Point(3, 3);
            this.collisionLabel.Margin = new System.Windows.Forms.Padding(3);
            this.collisionLabel.Name = "collisionLabel";
            this.collisionLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.collisionLabel.Size = new System.Drawing.Size(110, 23);
            this.collisionLabel.TabIndex = 0;
            this.collisionLabel.Text = "Collision:";
            //
            // _collision
            //
            this._collision.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._collision.Items.AddRange(new object[] {
            "Solid (walked on and shot at)",
            "Walls and floors only (blocks movement)",
            "Bullets only (and footsteps, zones)",
            "None"});
            this._collision.Location = new System.Drawing.Point(119, 3);
            this._collision.Name = "_collision";
            this._collision.Size = new System.Drawing.Size(300, 21);
            this._collision.TabIndex = 1;
            //
            // physicsRow
            //
            this.physicsRow.AutoSize = true;
            this.physicsRow.Controls.Add(this.physicsLabel);
            this.physicsRow.Controls.Add(this._physics);
            this.physicsRow.Controls.Add(this.physicsHint);
            this.physicsRow.Location = new System.Drawing.Point(6, 216);
            this.physicsRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.physicsRow.Name = "physicsRow";
            this.physicsRow.Size = new System.Drawing.Size(539, 29);
            this.physicsRow.TabIndex = 6;
            this.physicsRow.WrapContents = false;
            //
            // physicsLabel
            //
            this.physicsLabel.Location = new System.Drawing.Point(3, 3);
            this.physicsLabel.Margin = new System.Windows.Forms.Padding(3);
            this.physicsLabel.Name = "physicsLabel";
            this.physicsLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.physicsLabel.Size = new System.Drawing.Size(110, 23);
            this.physicsLabel.TabIndex = 0;
            this.physicsLabel.Text = "Physics props:";
            //
            // _physics
            //
            this._physics.Location = new System.Drawing.Point(119, 3);
            this._physics.Name = "_physics";
            this._physics.Size = new System.Drawing.Size(300, 20);
            this._physics.TabIndex = 1;
            //
            // physicsHint
            //
            this.physicsHint.AutoSize = true;
            this.physicsHint.Location = new System.Drawing.Point(425, 3);
            this.physicsHint.Margin = new System.Windows.Forms.Padding(3);
            this.physicsHint.Name = "physicsHint";
            this.physicsHint.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.physicsHint.Size = new System.Drawing.Size(111, 17);
            this.physicsHint.TabIndex = 2;
            this.physicsHint.Text = "(node names, * and ?)";
            //
            // excludeRow
            //
            this.excludeRow.AutoSize = true;
            this.excludeRow.Controls.Add(this.excludeLabel);
            this.excludeRow.Controls.Add(this._exclude);
            this.excludeRow.Controls.Add(this.excludeHint);
            this.excludeRow.Location = new System.Drawing.Point(6, 249);
            this.excludeRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.excludeRow.Name = "excludeRow";
            this.excludeRow.Size = new System.Drawing.Size(499, 29);
            this.excludeRow.TabIndex = 7;
            this.excludeRow.WrapContents = false;
            //
            // excludeLabel
            //
            this.excludeLabel.Location = new System.Drawing.Point(3, 3);
            this.excludeLabel.Margin = new System.Windows.Forms.Padding(3);
            this.excludeLabel.Name = "excludeLabel";
            this.excludeLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.excludeLabel.Size = new System.Drawing.Size(110, 23);
            this.excludeLabel.TabIndex = 0;
            this.excludeLabel.Text = "Leave out:";
            //
            // _exclude
            //
            this._exclude.Location = new System.Drawing.Point(119, 3);
            this._exclude.Name = "_exclude";
            this._exclude.Size = new System.Drawing.Size(300, 20);
            this._exclude.TabIndex = 1;
            //
            // excludeHint
            //
            this.excludeHint.AutoSize = true;
            this.excludeHint.Location = new System.Drawing.Point(425, 3);
            this.excludeHint.Margin = new System.Windows.Forms.Padding(3);
            this.excludeHint.Name = "excludeHint";
            this.excludeHint.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.excludeHint.Size = new System.Drawing.Size(71, 17);
            this.excludeHint.TabIndex = 2;
            this.excludeHint.Text = "(node names)";
            //
            // lightsRow
            //
            this.lightsRow.AutoSize = true;
            this.lightsRow.Controls.Add(this.lightsLabel);
            this.lightsRow.Controls.Add(this._lights);
            this.lightsRow.Controls.Add(this.brightnessLabel);
            this.lightsRow.Controls.Add(this._lightScale);
            this.lightsRow.Controls.Add(this._shadows);
            this.lightsRow.Location = new System.Drawing.Point(6, 282);
            this.lightsRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.lightsRow.Name = "lightsRow";
            this.lightsRow.Size = new System.Drawing.Size(607, 29);
            this.lightsRow.TabIndex = 8;
            this.lightsRow.WrapContents = false;
            //
            // lightsLabel
            //
            this.lightsLabel.Location = new System.Drawing.Point(3, 3);
            this.lightsLabel.Margin = new System.Windows.Forms.Padding(3);
            this.lightsLabel.Name = "lightsLabel";
            this.lightsLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.lightsLabel.Size = new System.Drawing.Size(110, 23);
            this.lightsLabel.TabIndex = 0;
            this.lightsLabel.Text = "Lights:";
            //
            // _lights
            //
            this._lights.AutoSize = true;
            this._lights.Checked = true;
            this._lights.CheckState = System.Windows.Forms.CheckState.Checked;
            this._lights.Location = new System.Drawing.Point(119, 3);
            this._lights.Name = "_lights";
            this._lights.Size = new System.Drawing.Size(123, 17);
            this._lights.TabIndex = 1;
            this._lights.Text = "Import the file\'s lights";
            this._lights.CheckedChanged += new System.EventHandler(this._lights_CheckedChanged);
            //
            // brightnessLabel
            //
            this.brightnessLabel.AutoSize = true;
            this.brightnessLabel.Location = new System.Drawing.Point(248, 3);
            this.brightnessLabel.Margin = new System.Windows.Forms.Padding(3);
            this.brightnessLabel.Name = "brightnessLabel";
            this.brightnessLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.brightnessLabel.Size = new System.Drawing.Size(64, 17);
            this.brightnessLabel.TabIndex = 2;
            this.brightnessLabel.Text = "Brightness x";
            //
            // _lightScale
            //
            this._lightScale.DecimalPlaces = 2;
            this._lightScale.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this._lightScale.Location = new System.Drawing.Point(318, 3);
            this._lightScale.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this._lightScale.Name = "_lightScale";
            this._lightScale.Size = new System.Drawing.Size(80, 20);
            this._lightScale.TabIndex = 3;
            this._lightScale.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            //
            // _shadows
            //
            this._shadows.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._shadows.Items.AddRange(new object[] {
            "Spot lights cast shadows",
            "Every light casts shadows",
            "No shadows"});
            this._shadows.Location = new System.Drawing.Point(404, 3);
            this._shadows.Name = "_shadows";
            this._shadows.Size = new System.Drawing.Size(200, 21);
            this._shadows.TabIndex = 4;
            //
            // sunRow
            //
            this.sunRow.AutoSize = true;
            this.sunRow.Controls.Add(this.sunSpacer);
            this.sunRow.Controls.Add(this._sun);
            this.sunRow.Location = new System.Drawing.Point(6, 315);
            this.sunRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.sunRow.Name = "sunRow";
            this.sunRow.Size = new System.Drawing.Size(392, 29);
            this.sunRow.TabIndex = 9;
            this.sunRow.WrapContents = false;
            //
            // sunSpacer
            //
            this.sunSpacer.Location = new System.Drawing.Point(3, 3);
            this.sunSpacer.Margin = new System.Windows.Forms.Padding(3);
            this.sunSpacer.Name = "sunSpacer";
            this.sunSpacer.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.sunSpacer.Size = new System.Drawing.Size(110, 23);
            this.sunSpacer.TabIndex = 0;
            //
            // _sun
            //
            this._sun.AutoSize = true;
            this._sun.Location = new System.Drawing.Point(119, 3);
            this._sun.Name = "_sun";
            this._sun.Size = new System.Drawing.Size(270, 17);
            this._sun.TabIndex = 1;
            this._sun.Text = "Build a sun from a directional light (Outdoor Lighting)";
            //
            // mergeRow
            //
            this.mergeRow.AutoSize = true;
            this.mergeRow.Controls.Add(this.mergeSpacer);
            this.mergeRow.Controls.Add(this._merge);
            this.mergeRow.Location = new System.Drawing.Point(6, 348);
            this.mergeRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.mergeRow.Name = "mergeRow";
            this.mergeRow.Size = new System.Drawing.Size(361, 29);
            this.mergeRow.TabIndex = 10;
            this.mergeRow.WrapContents = false;
            //
            // mergeSpacer
            //
            this.mergeSpacer.Location = new System.Drawing.Point(3, 3);
            this.mergeSpacer.Margin = new System.Windows.Forms.Padding(3);
            this.mergeSpacer.Name = "mergeSpacer";
            this.mergeSpacer.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.mergeSpacer.Size = new System.Drawing.Size(110, 23);
            this.mergeSpacer.TabIndex = 0;
            //
            // _merge
            //
            this._merge.AutoSize = true;
            this._merge.Checked = true;
            this._merge.CheckState = System.Windows.Forms.CheckState.Checked;
            this._merge.Location = new System.Drawing.Point(119, 3);
            this._merge.Name = "_merge";
            this._merge.Size = new System.Drawing.Size(239, 17);
            this._merge.TabIndex = 1;
            this._merge.Text = "Meshes with the same content share a model";
            //
            // compactRow
            //
            this.compactRow.AutoSize = true;
            this.compactRow.Controls.Add(this.compactSpacer);
            this.compactRow.Controls.Add(this._compact);
            this.compactRow.Location = new System.Drawing.Point(6, 381);
            this.compactRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.compactRow.Name = "compactRow";
            this.compactRow.Size = new System.Drawing.Size(415, 29);
            this.compactRow.TabIndex = 11;
            this.compactRow.WrapContents = false;
            //
            // compactSpacer
            //
            this.compactSpacer.Location = new System.Drawing.Point(3, 3);
            this.compactSpacer.Margin = new System.Windows.Forms.Padding(3);
            this.compactSpacer.Name = "compactSpacer";
            this.compactSpacer.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.compactSpacer.Size = new System.Drawing.Size(110, 23);
            this.compactSpacer.TabIndex = 0;
            //
            // _compact
            //
            this._compact.AutoSize = true;
            this._compact.Location = new System.Drawing.Point(119, 3);
            this._compact.Name = "_compact";
            this._compact.Size = new System.Drawing.Size(293, 17);
            this._compact.TabIndex = 1;
            this._compact.Text = "Compact textures (DXT1/DXT5: half the texture memory)";
            //
            // _report
            //
            this._report.Dock = System.Windows.Forms.DockStyle.Fill;
            this._report.Font = new System.Drawing.Font("Courier New", 8.5F);
            this._report.Location = new System.Drawing.Point(0, 0);
            this._report.Multiline = true;
            this._report.Name = "_report";
            this._report.ReadOnly = true;
            this._report.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._report.Size = new System.Drawing.Size(924, 337);
            this._report.TabIndex = 0;
            //
            // buttonsPanel
            //
            this.buttonsPanel.AutoSize = true;
            this.buttonsPanel.Controls.Add(this._dryRun);
            this.buttonsPanel.Controls.Add(this._import);
            this.buttonsPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.buttonsPanel.Location = new System.Drawing.Point(0, 767);
            this.buttonsPanel.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.buttonsPanel.Name = "buttonsPanel";
            this.buttonsPanel.Padding = new System.Windows.Forms.Padding(6, 2, 0, 2);
            this.buttonsPanel.Size = new System.Drawing.Size(924, 33);
            this.buttonsPanel.TabIndex = 1;
            this.buttonsPanel.WrapContents = false;
            //
            // _dryRun
            //
            this._dryRun.AutoSize = true;
            this._dryRun.Location = new System.Drawing.Point(9, 5);
            this._dryRun.Name = "_dryRun";
            this._dryRun.Size = new System.Drawing.Size(75, 23);
            this._dryRun.TabIndex = 0;
            this._dryRun.Text = "Dry Run";
            this._dryRun.Click += new System.EventHandler(this._dryRun_Click);
            //
            // _import
            //
            this._import.AutoSize = true;
            this._import.Location = new System.Drawing.Point(90, 5);
            this._import.Name = "_import";
            this._import.Size = new System.Drawing.Size(75, 23);
            this._import.TabIndex = 1;
            this._import.Text = "Import";
            this._import.Click += new System.EventHandler(this._import_Click);
            //
            // _status
            //
            this._status.AutoSize = true;
            this._status.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._status.Location = new System.Drawing.Point(0, 800);
            this._status.Name = "_status";
            this._status.Padding = new System.Windows.Forms.Padding(6, 4, 0, 4);
            this._status.Size = new System.Drawing.Size(469, 21);
            this._status.TabIndex = 2;
            this._status.Text = "Dry Run only reports; Import changes the level (one undo step; Save and Build to see it in game).";
            //
            // SceneImportWindow
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(924, 821);
            this.Controls.Add(this._split);
            this.Controls.Add(this.buttonsPanel);
            this.Controls.Add(this._status);
            this.MinimumSize = new System.Drawing.Size(760, 600);
            this.Name = "SceneImportWindow";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Import Scene";
            this.Load += new System.EventHandler(this.SceneImportWindow_Load);
            this._split.Panel1.ResumeLayout(false);
            this._split.Panel2.ResumeLayout(false);
            this._split.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._split)).EndInit();
            this._split.ResumeLayout(false);
            this.optionsPanel.ResumeLayout(false);
            this.optionsPanel.PerformLayout();
            this.fileRow.ResumeLayout(false);
            this.fileRow.PerformLayout();
            this.nameRow.ResumeLayout(false);
            this.nameRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._scale)).EndInit();
            this.placeRow.ResumeLayout(false);
            this.placeRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._px)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._py)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._pz)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._yaw)).EndInit();
            this.groupRow.ResumeLayout(false);
            this.collisionRow.ResumeLayout(false);
            this.physicsRow.ResumeLayout(false);
            this.physicsRow.PerformLayout();
            this.excludeRow.ResumeLayout(false);
            this.excludeRow.PerformLayout();
            this.lightsRow.ResumeLayout(false);
            this.lightsRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._lightScale)).EndInit();
            this.sunRow.ResumeLayout(false);
            this.sunRow.PerformLayout();
            this.mergeRow.ResumeLayout(false);
            this.mergeRow.PerformLayout();
            this.compactRow.ResumeLayout(false);
            this.compactRow.PerformLayout();
            this.buttonsPanel.ResumeLayout(false);
            this.buttonsPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer _split;
        private System.Windows.Forms.FlowLayoutPanel optionsPanel;
        private System.Windows.Forms.Label noteLabel;
        private System.Windows.Forms.FlowLayoutPanel fileRow;
        private System.Windows.Forms.Label fileLabel;
        private System.Windows.Forms.TextBox _file;
        private System.Windows.Forms.Button _browse;
        private System.Windows.Forms.FlowLayoutPanel nameRow;
        private System.Windows.Forms.Label nameLabel;
        private System.Windows.Forms.TextBox _name;
        private System.Windows.Forms.Label scaleLabel;
        private System.Windows.Forms.NumericUpDown _scale;
        private System.Windows.Forms.FlowLayoutPanel placeRow;
        private System.Windows.Forms.Label placeLabel;
        private System.Windows.Forms.NumericUpDown _px;
        private System.Windows.Forms.NumericUpDown _py;
        private System.Windows.Forms.NumericUpDown _pz;
        private System.Windows.Forms.Label turnLabel;
        private System.Windows.Forms.NumericUpDown _yaw;
        private System.Windows.Forms.FlowLayoutPanel groupRow;
        private System.Windows.Forms.Label groupLabel;
        private System.Windows.Forms.ComboBox _group;
        private System.Windows.Forms.FlowLayoutPanel collisionRow;
        private System.Windows.Forms.Label collisionLabel;
        private System.Windows.Forms.ComboBox _collision;
        private System.Windows.Forms.FlowLayoutPanel physicsRow;
        private System.Windows.Forms.Label physicsLabel;
        private System.Windows.Forms.TextBox _physics;
        private System.Windows.Forms.Label physicsHint;
        private System.Windows.Forms.FlowLayoutPanel excludeRow;
        private System.Windows.Forms.Label excludeLabel;
        private System.Windows.Forms.TextBox _exclude;
        private System.Windows.Forms.Label excludeHint;
        private System.Windows.Forms.FlowLayoutPanel lightsRow;
        private System.Windows.Forms.Label lightsLabel;
        private System.Windows.Forms.CheckBox _lights;
        private System.Windows.Forms.Label brightnessLabel;
        private System.Windows.Forms.NumericUpDown _lightScale;
        private System.Windows.Forms.ComboBox _shadows;
        private System.Windows.Forms.FlowLayoutPanel sunRow;
        private System.Windows.Forms.Label sunSpacer;
        private System.Windows.Forms.CheckBox _sun;
        private System.Windows.Forms.FlowLayoutPanel mergeRow;
        private System.Windows.Forms.Label mergeSpacer;
        private System.Windows.Forms.CheckBox _merge;
        private System.Windows.Forms.FlowLayoutPanel compactRow;
        private System.Windows.Forms.Label compactSpacer;
        private System.Windows.Forms.CheckBox _compact;
        private System.Windows.Forms.TextBox _report;
        private System.Windows.Forms.FlowLayoutPanel buttonsPanel;
        private System.Windows.Forms.Button _dryRun;
        private System.Windows.Forms.Button _import;
        private System.Windows.Forms.Label _status;
    }
}

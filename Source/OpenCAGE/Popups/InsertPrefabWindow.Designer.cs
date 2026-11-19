namespace OpenCAGE.Prefabs
{
    partial class InsertPrefabWindow
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
            this._tree = new System.Windows.Forms.TreeView();
            this._rightPanel = new System.Windows.Forms.Panel();
            this._about = new System.Windows.Forms.TextBox();
            this._optionsPanel = new System.Windows.Forms.Panel();
            this._nameLabel = new System.Windows.Forms.Label();
            this._name = new System.Windows.Forms.TextBox();
            this._characterLabel = new System.Windows.Forms.Label();
            this._character = new System.Windows.Forms.ComboBox();
            this._boxLabel = new System.Windows.Forms.Label();
            this._boxX = new System.Windows.Forms.NumericUpDown();
            this._boxY = new System.Windows.Forms.NumericUpDown();
            this._boxZ = new System.Windows.Forms.NumericUpDown();
            this._status = new System.Windows.Forms.Label();
            this._insert = new System.Windows.Forms.Button();
            this._folder = new System.Windows.Forms.Button();
            this._title = new System.Windows.Forms.Label();
            this._footer = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this._split)).BeginInit();
            this._split.Panel1.SuspendLayout();
            this._split.Panel2.SuspendLayout();
            this._split.SuspendLayout();
            this._rightPanel.SuspendLayout();
            this._optionsPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._boxX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._boxY)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._boxZ)).BeginInit();
            this.SuspendLayout();
            //
            // _split
            //
            this._split.Dock = System.Windows.Forms.DockStyle.Fill;
            this._split.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this._split.Location = new System.Drawing.Point(0, 0);
            this._split.Name = "_split";
            //
            // _split.Panel1
            //
            this._split.Panel1.Controls.Add(this._tree);
            this._split.Panel1.Padding = new System.Windows.Forms.Padding(8, 8, 0, 8);
            //
            // _split.Panel2
            //
            this._split.Panel2.Controls.Add(this._rightPanel);
            this._split.Size = new System.Drawing.Size(884, 499);
            this._split.SplitterDistance = 230;
            this._split.TabIndex = 0;
            //
            // _tree
            //
            this._tree.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tree.FullRowSelect = true;
            this._tree.HideSelection = false;
            this._tree.Location = new System.Drawing.Point(8, 8);
            this._tree.Name = "_tree";
            this._tree.ShowLines = false;
            this._tree.Size = new System.Drawing.Size(222, 483);
            this._tree.TabIndex = 0;
            this._tree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this._tree_AfterSelect);
            this._tree.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this._tree_NodeMouseDoubleClick);
            //
            // _rightPanel
            //
            this._rightPanel.Controls.Add(this._about);
            this._rightPanel.Controls.Add(this._optionsPanel);
            this._rightPanel.Controls.Add(this._title);
            this._rightPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._rightPanel.Location = new System.Drawing.Point(0, 0);
            this._rightPanel.Name = "_rightPanel";
            this._rightPanel.Padding = new System.Windows.Forms.Padding(8);
            this._rightPanel.Size = new System.Drawing.Size(650, 499);
            this._rightPanel.TabIndex = 0;
            //
            // _about
            //
            this._about.Dock = System.Windows.Forms.DockStyle.Fill;
            this._about.Location = new System.Drawing.Point(8, 34);
            this._about.Multiline = true;
            this._about.Name = "_about";
            this._about.ReadOnly = true;
            this._about.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._about.Size = new System.Drawing.Size(634, 307);
            this._about.TabIndex = 0;
            //
            // _optionsPanel
            //
            this._optionsPanel.Controls.Add(this._nameLabel);
            this._optionsPanel.Controls.Add(this._name);
            this._optionsPanel.Controls.Add(this._characterLabel);
            this._optionsPanel.Controls.Add(this._character);
            this._optionsPanel.Controls.Add(this._boxLabel);
            this._optionsPanel.Controls.Add(this._boxX);
            this._optionsPanel.Controls.Add(this._boxY);
            this._optionsPanel.Controls.Add(this._boxZ);
            this._optionsPanel.Controls.Add(this._status);
            this._optionsPanel.Controls.Add(this._insert);
            this._optionsPanel.Controls.Add(this._folder);
            this._optionsPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._optionsPanel.Location = new System.Drawing.Point(8, 341);
            this._optionsPanel.Name = "_optionsPanel";
            this._optionsPanel.Size = new System.Drawing.Size(634, 150);
            this._optionsPanel.TabIndex = 1;
            this._optionsPanel.Resize += new System.EventHandler(this._optionsPanel_Resize);
            //
            // _nameLabel
            //
            this._nameLabel.AutoSize = true;
            this._nameLabel.Location = new System.Drawing.Point(0, 12);
            this._nameLabel.Name = "_nameLabel";
            this._nameLabel.TabIndex = 0;
            this._nameLabel.Text = "Name:";
            //
            // _name
            //
            this._name.Location = new System.Drawing.Point(110, 8);
            this._name.Name = "_name";
            this._name.Size = new System.Drawing.Size(300, 20);
            this._name.TabIndex = 1;
            //
            // _characterLabel
            //
            this._characterLabel.AutoSize = true;
            this._characterLabel.Location = new System.Drawing.Point(0, 41);
            this._characterLabel.Name = "_characterLabel";
            this._characterLabel.TabIndex = 2;
            this._characterLabel.Text = "Character:";
            //
            // _character
            //
            this._character.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._character.Location = new System.Drawing.Point(110, 37);
            this._character.Name = "_character";
            this._character.Size = new System.Drawing.Size(420, 21);
            this._character.TabIndex = 3;
            //
            // _boxLabel
            //
            this._boxLabel.AutoSize = true;
            this._boxLabel.Location = new System.Drawing.Point(0, 41);
            this._boxLabel.Name = "_boxLabel";
            this._boxLabel.TabIndex = 4;
            this._boxLabel.Text = "Box half size (m):";
            //
            // _boxX
            //
            this._boxX.DecimalPlaces = 2;
            this._boxX.Increment = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this._boxX.Location = new System.Drawing.Point(110, 37);
            this._boxX.Maximum = new decimal(new int[] {
            500,
            0,
            0,
            0});
            this._boxX.Minimum = new decimal(new int[] {
            5,
            0,
            0,
            131072});
            this._boxX.Name = "_boxX";
            this._boxX.Size = new System.Drawing.Size(72, 20);
            this._boxX.TabIndex = 5;
            this._boxX.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            //
            // _boxY
            //
            this._boxY.DecimalPlaces = 2;
            this._boxY.Increment = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this._boxY.Location = new System.Drawing.Point(190, 37);
            this._boxY.Maximum = new decimal(new int[] {
            500,
            0,
            0,
            0});
            this._boxY.Minimum = new decimal(new int[] {
            5,
            0,
            0,
            131072});
            this._boxY.Name = "_boxY";
            this._boxY.Size = new System.Drawing.Size(72, 20);
            this._boxY.TabIndex = 6;
            this._boxY.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            //
            // _boxZ
            //
            this._boxZ.DecimalPlaces = 2;
            this._boxZ.Increment = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this._boxZ.Location = new System.Drawing.Point(270, 37);
            this._boxZ.Maximum = new decimal(new int[] {
            500,
            0,
            0,
            0});
            this._boxZ.Minimum = new decimal(new int[] {
            5,
            0,
            0,
            131072});
            this._boxZ.Name = "_boxZ";
            this._boxZ.Size = new System.Drawing.Size(72, 20);
            this._boxZ.TabIndex = 7;
            this._boxZ.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            //
            // _status
            //
            this._status.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this._status.Location = new System.Drawing.Point(0, 70);
            this._status.Name = "_status";
            this._status.Size = new System.Drawing.Size(634, 40);
            this._status.TabIndex = 8;
            //
            // _insert
            //
            this._insert.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._insert.Location = new System.Drawing.Point(534, 120);
            this._insert.Name = "_insert";
            this._insert.Size = new System.Drawing.Size(100, 28);
            this._insert.TabIndex = 9;
            this._insert.Text = "Insert";
            this._insert.Click += new System.EventHandler(this._insert_Click);
            //
            // _folder
            //
            this._folder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this._folder.AutoSize = true;
            this._folder.Location = new System.Drawing.Point(0, 120);
            this._folder.Name = "_folder";
            this._folder.Size = new System.Drawing.Size(75, 28);
            this._folder.TabIndex = 10;
            this._folder.Text = "Open Prefab Folder";
            this._folder.Click += new System.EventHandler(this._folder_Click);
            //
            // _title
            //
            this._title.Dock = System.Windows.Forms.DockStyle.Top;
            this._title.Font = new System.Drawing.Font("Microsoft Sans Serif", 11F, System.Drawing.FontStyle.Bold);
            this._title.Location = new System.Drawing.Point(8, 8);
            this._title.Name = "_title";
            this._title.Size = new System.Drawing.Size(634, 26);
            this._title.TabIndex = 2;
            //
            // _footer
            //
            this._footer.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._footer.Location = new System.Drawing.Point(0, 499);
            this._footer.Name = "_footer";
            this._footer.Padding = new System.Windows.Forms.Padding(8, 4, 8, 0);
            this._footer.Size = new System.Drawing.Size(884, 22);
            this._footer.TabIndex = 1;
            this._footer.Text = "Add your own: export composites as a package (File > Export Composites > To Disk) into the Prefabs folder, and they are listed under My prefabs.";
            //
            // InsertPrefabWindow
            //
            this.AcceptButton = this._insert;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(884, 521);
            this.Controls.Add(this._split);
            this.Controls.Add(this._footer);
            this.MinimumSize = new System.Drawing.Size(700, 440);
            this.Name = "InsertPrefabWindow";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Insert Prefab";
            this._split.Panel1.ResumeLayout(false);
            this._split.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._split)).EndInit();
            this._split.ResumeLayout(false);
            this._rightPanel.ResumeLayout(false);
            this._rightPanel.PerformLayout();
            this._optionsPanel.ResumeLayout(false);
            this._optionsPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._boxX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._boxY)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._boxZ)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer _split;
        private System.Windows.Forms.TreeView _tree;
        private System.Windows.Forms.Panel _rightPanel;
        private System.Windows.Forms.TextBox _about;
        private System.Windows.Forms.Panel _optionsPanel;
        private System.Windows.Forms.Label _nameLabel;
        private System.Windows.Forms.TextBox _name;
        private System.Windows.Forms.Label _characterLabel;
        private System.Windows.Forms.ComboBox _character;
        private System.Windows.Forms.Label _boxLabel;
        private System.Windows.Forms.NumericUpDown _boxX;
        private System.Windows.Forms.NumericUpDown _boxY;
        private System.Windows.Forms.NumericUpDown _boxZ;
        private System.Windows.Forms.Label _status;
        private System.Windows.Forms.Button _insert;
        private System.Windows.Forms.Button _folder;
        private System.Windows.Forms.Label _title;
        private System.Windows.Forms.Label _footer;
    }
}

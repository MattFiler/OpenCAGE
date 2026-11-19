#if ENABLE_MOD_PACKAGES
namespace OpenCAGE.Popups
{
    partial class ModManagerForm
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
            this._header = new System.Windows.Forms.Panel();
            this._copiesNote = new System.Windows.Forms.Label();
            this._stateRow = new System.Windows.Forms.FlowLayoutPanel();
            this._installState = new System.Windows.Forms.Label();
            this._installAction = new System.Windows.Forms.LinkLabel();
            this._recheck = new System.Windows.Forms.LinkLabel();
            this._headline = new System.Windows.Forms.Label();
            this._split = new System.Windows.Forms.SplitContainer();
            this._listPanel = new System.Windows.Forms.Panel();
            this.modList = new System.Windows.Forms.ListView();
            this._modColumn = new System.Windows.Forms.ColumnHeader();
            this._versionColumn = new System.Windows.Forms.ColumnHeader();
            this._authorColumn = new System.Windows.Forms.ColumnHeader();
            this._typeColumn = new System.Windows.Forms.ColumnHeader();
            this._statusColumn = new System.Windows.Forms.ColumnHeader();
            this._orderTip = new System.Windows.Forms.Label();
            this._detailPanel = new System.Windows.Forms.Panel();
            this._details = new System.Windows.Forms.TextBox();
            this._detailByline = new System.Windows.Forms.Label();
            this._detailTitle = new System.Windows.Forms.Label();
            this._preview = new System.Windows.Forms.PictureBox();
            this._statusLabel = new System.Windows.Forms.Label();
            this._buttonBar = new System.Windows.Forms.Panel();
            this._leftButtons = new System.Windows.Forms.FlowLayoutPanel();
            this._addButton = new System.Windows.Forms.Button();
            this._removeButton = new System.Windows.Forms.Button();
            this._upButton = new System.Windows.Forms.Button();
            this._downButton = new System.Windows.Forms.Button();
            this._rightButtons = new System.Windows.Forms.FlowLayoutPanel();
            this._applyButton = new System.Windows.Forms.Button();
            this.launchModButton = new System.Windows.Forms.Button();
            this._repairButton = new System.Windows.Forms.Button();
            this._createButton = new System.Windows.Forms.Button();
            this._copiesNoteTip = new System.Windows.Forms.ToolTip(this.components);
            this._launchTips = new System.Windows.Forms.ToolTip(this.components);
            this._header.SuspendLayout();
            this._stateRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._split)).BeginInit();
            this._split.Panel1.SuspendLayout();
            this._split.Panel2.SuspendLayout();
            this._split.SuspendLayout();
            this._listPanel.SuspendLayout();
            this._detailPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._preview)).BeginInit();
            this._buttonBar.SuspendLayout();
            this._leftButtons.SuspendLayout();
            this._rightButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // _header
            // 
            this._header.Controls.Add(this._copiesNote);
            this._header.Controls.Add(this._stateRow);
            this._header.Controls.Add(this._headline);
            this._header.Dock = System.Windows.Forms.DockStyle.Top;
            this._header.Location = new System.Drawing.Point(0, 0);
            this._header.Name = "_header";
            this._header.Padding = new System.Windows.Forms.Padding(12, 8, 12, 6);
            this._header.Size = new System.Drawing.Size(1024, 64);
            this._header.TabIndex = 3;
            // 
            // _copiesNote
            // 
            this._copiesNote.AutoEllipsis = true;
            this._copiesNote.Dock = System.Windows.Forms.DockStyle.Top;
            this._copiesNote.Location = new System.Drawing.Point(12, 56);
            this._copiesNote.Name = "_copiesNote";
            this._copiesNote.Padding = new System.Windows.Forms.Padding(0, 2, 0, 0);
            this._copiesNote.Size = new System.Drawing.Size(1000, 20);
            this._copiesNote.TabIndex = 0;
            this._copiesNote.Visible = false;
            // 
            // _stateRow
            // 
            this._stateRow.Controls.Add(this._installState);
            this._stateRow.Controls.Add(this._installAction);
            this._stateRow.Controls.Add(this._recheck);
            this._stateRow.Dock = System.Windows.Forms.DockStyle.Top;
            this._stateRow.Location = new System.Drawing.Point(12, 32);
            this._stateRow.Name = "_stateRow";
            this._stateRow.Size = new System.Drawing.Size(1000, 24);
            this._stateRow.TabIndex = 1;
            this._stateRow.WrapContents = false;
            // 
            // _installState
            // 
            this._installState.AutoSize = true;
            this._installState.Location = new System.Drawing.Point(0, 4);
            this._installState.Margin = new System.Windows.Forms.Padding(0, 4, 6, 0);
            this._installState.Name = "_installState";
            this._installState.Size = new System.Drawing.Size(141, 17);
            this._installState.TabIndex = 0;
            this._installState.Text = "Checking your game files...";
            // 
            // _installAction
            // 
            this._installAction.AutoSize = true;
            this._installAction.Location = new System.Drawing.Point(147, 4);
            this._installAction.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this._installAction.Name = "_installAction";
            this._installAction.Size = new System.Drawing.Size(0, 17);
            this._installAction.TabIndex = 1;
            this._installAction.Visible = false;
            this._installAction.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this._installAction_LinkClicked);
            // 
            // _recheck
            // 
            this._recheck.AutoSize = true;
            this._recheck.Location = new System.Drawing.Point(153, 4);
            this._recheck.Margin = new System.Windows.Forms.Padding(6, 4, 0, 0);
            this._recheck.Name = "_recheck";
            this._recheck.Size = new System.Drawing.Size(116, 17);
            this._recheck.TabIndex = 2;
            this._recheck.Text = "Check every file again";
            this._recheck.Visible = false;
            this._recheck.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this._recheck_LinkClicked);
            // 
            // _headline
            // 
            this._headline.Dock = System.Windows.Forms.DockStyle.Top;
            this._headline.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this._headline.Location = new System.Drawing.Point(12, 8);
            this._headline.Name = "_headline";
            this._headline.Size = new System.Drawing.Size(1000, 24);
            this._headline.TabIndex = 2;
            this._headline.Text = "Tick the mods you want to play with, put them in order, then click Apply.";
            // 
            // _split
            // 
            this._split.Dock = System.Windows.Forms.DockStyle.Fill;
            this._split.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this._split.Location = new System.Drawing.Point(0, 64);
            this._split.Name = "_split";
            // 
            // _split.Panel1
            // 
            this._split.Panel1.Controls.Add(this._listPanel);
            // 
            // _split.Panel2
            // 
            this._split.Panel2.Controls.Add(this._detailPanel);
            this._split.Size = new System.Drawing.Size(1024, 489);
            this._split.SplitterDistance = 604;
            this._split.TabIndex = 0;
            // 
            // _listPanel
            // 
            this._listPanel.Controls.Add(this.modList);
            this._listPanel.Controls.Add(this._orderTip);
            this._listPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._listPanel.Location = new System.Drawing.Point(0, 0);
            this._listPanel.Name = "_listPanel";
            this._listPanel.Size = new System.Drawing.Size(604, 489);
            this._listPanel.TabIndex = 0;
            // 
            // modList
            // 
            this.modList.AllowDrop = true;
            this.modList.CheckBoxes = true;
            this.modList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._modColumn,
            this._versionColumn,
            this._authorColumn,
            this._typeColumn,
            this._statusColumn});
            this.modList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.modList.FullRowSelect = true;
            this.modList.HideSelection = false;
            this.modList.Location = new System.Drawing.Point(0, 0);
            this.modList.MultiSelect = false;
            this.modList.Name = "modList";
            this.modList.Size = new System.Drawing.Size(604, 455);
            this.modList.TabIndex = 0;
            this.modList.View = System.Windows.Forms.View.Details;
            this.modList.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this.modList_ItemChecked);
            this.modList.ItemDrag += new System.Windows.Forms.ItemDragEventHandler(this.modList_ItemDrag);
            this.modList.SelectedIndexChanged += new System.EventHandler(this.modList_SelectedIndexChanged);
            this.modList.DragDrop += new System.Windows.Forms.DragEventHandler(this.OnListDragDrop);
            this.modList.DragEnter += new System.Windows.Forms.DragEventHandler(this.OnListDragEnter);
            this.modList.DragOver += new System.Windows.Forms.DragEventHandler(this.OnListDragOver);
            // 
            // _modColumn
            // 
            this._modColumn.Text = "Mod";
            this._modColumn.Width = 230;
            // 
            // _versionColumn
            // 
            this._versionColumn.Text = "Version";
            this._versionColumn.Width = 64;
            // 
            // _authorColumn
            // 
            this._authorColumn.Text = "Author";
            this._authorColumn.Width = 110;
            // 
            // _typeColumn
            // 
            this._typeColumn.Text = "Type";
            this._typeColumn.Width = 90;
            // 
            // _statusColumn
            // 
            this._statusColumn.Text = "Status";
            this._statusColumn.Width = 140;
            // 
            // _orderTip
            // 
            this._orderTip.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._orderTip.Location = new System.Drawing.Point(0, 455);
            this._orderTip.Name = "_orderTip";
            this._orderTip.Padding = new System.Windows.Forms.Padding(2, 4, 2, 0);
            this._orderTip.Size = new System.Drawing.Size(604, 34);
            this._orderTip.TabIndex = 1;
            this._orderTip.Text = "Mods lower in the list win when two of them change the very same thing. Drag a mod, or use Move Up / Move Down, to change the order.";
            // 
            // _detailPanel
            // 
            this._detailPanel.Controls.Add(this._details);
            this._detailPanel.Controls.Add(this._detailByline);
            this._detailPanel.Controls.Add(this._detailTitle);
            this._detailPanel.Controls.Add(this._preview);
            this._detailPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._detailPanel.Location = new System.Drawing.Point(0, 0);
            this._detailPanel.Name = "_detailPanel";
            this._detailPanel.Padding = new System.Windows.Forms.Padding(10, 4, 6, 4);
            this._detailPanel.Size = new System.Drawing.Size(416, 489);
            this._detailPanel.TabIndex = 0;
            // 
            // _details
            // 
            this._details.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this._details.Dock = System.Windows.Forms.DockStyle.Fill;
            this._details.Location = new System.Drawing.Point(10, 52);
            this._details.Multiline = true;
            this._details.Name = "_details";
            this._details.ReadOnly = true;
            this._details.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._details.Size = new System.Drawing.Size(400, 433);
            this._details.TabIndex = 0;
            // 
            // _detailByline
            // 
            this._detailByline.AutoEllipsis = true;
            this._detailByline.Dock = System.Windows.Forms.DockStyle.Top;
            this._detailByline.Location = new System.Drawing.Point(10, 32);
            this._detailByline.Name = "_detailByline";
            this._detailByline.Size = new System.Drawing.Size(400, 20);
            this._detailByline.TabIndex = 1;
            // 
            // _detailTitle
            // 
            this._detailTitle.AutoEllipsis = true;
            this._detailTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this._detailTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold);
            this._detailTitle.Location = new System.Drawing.Point(10, 4);
            this._detailTitle.Name = "_detailTitle";
            this._detailTitle.Size = new System.Drawing.Size(400, 28);
            this._detailTitle.TabIndex = 2;
            // 
            // _preview
            // 
            this._preview.Dock = System.Windows.Forms.DockStyle.Top;
            this._preview.Location = new System.Drawing.Point(10, 4);
            this._preview.Name = "_preview";
            this._preview.Size = new System.Drawing.Size(400, 170);
            this._preview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this._preview.TabIndex = 3;
            this._preview.TabStop = false;
            this._preview.Visible = false;
            // 
            // _statusLabel
            // 
            this._statusLabel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._statusLabel.Location = new System.Drawing.Point(0, 553);
            this._statusLabel.Name = "_statusLabel";
            this._statusLabel.Padding = new System.Windows.Forms.Padding(10, 0, 0, 0);
            this._statusLabel.Size = new System.Drawing.Size(1024, 22);
            this._statusLabel.TabIndex = 1;
            this._statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _buttonBar
            // 
            this._buttonBar.Controls.Add(this._leftButtons);
            this._buttonBar.Controls.Add(this._rightButtons);
            this._buttonBar.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._buttonBar.Location = new System.Drawing.Point(0, 575);
            this._buttonBar.Name = "_buttonBar";
            this._buttonBar.Size = new System.Drawing.Size(1024, 46);
            this._buttonBar.TabIndex = 2;
            // 
            // _leftButtons
            // 
            this._leftButtons.AutoSize = true;
            this._leftButtons.Controls.Add(this._addButton);
            this._leftButtons.Controls.Add(this._removeButton);
            this._leftButtons.Controls.Add(this._upButton);
            this._leftButtons.Controls.Add(this._downButton);
            this._leftButtons.Dock = System.Windows.Forms.DockStyle.Left;
            this._leftButtons.Location = new System.Drawing.Point(0, 0);
            this._leftButtons.Name = "_leftButtons";
            this._leftButtons.Padding = new System.Windows.Forms.Padding(6, 6, 0, 6);
            this._leftButtons.Size = new System.Drawing.Size(330, 46);
            this._leftButtons.TabIndex = 0;
            this._leftButtons.WrapContents = false;
            // 
            // _addButton
            // 
            this._addButton.AutoSize = true;
            this._addButton.Location = new System.Drawing.Point(9, 9);
            this._addButton.MinimumSize = new System.Drawing.Size(0, 30);
            this._addButton.Name = "_addButton";
            this._addButton.Size = new System.Drawing.Size(75, 30);
            this._addButton.TabIndex = 0;
            this._addButton.Text = "Add Mod...";
            this._addButton.Click += new System.EventHandler(this._addButton_Click);
            // 
            // _removeButton
            // 
            this._removeButton.AutoSize = true;
            this._removeButton.Location = new System.Drawing.Point(90, 9);
            this._removeButton.MinimumSize = new System.Drawing.Size(0, 30);
            this._removeButton.Name = "_removeButton";
            this._removeButton.Size = new System.Drawing.Size(75, 30);
            this._removeButton.TabIndex = 1;
            this._removeButton.Text = "Remove";
            this._removeButton.Click += new System.EventHandler(this._removeButton_Click);
            // 
            // _upButton
            // 
            this._upButton.AutoSize = true;
            this._upButton.Location = new System.Drawing.Point(171, 9);
            this._upButton.MinimumSize = new System.Drawing.Size(0, 30);
            this._upButton.Name = "_upButton";
            this._upButton.Size = new System.Drawing.Size(75, 30);
            this._upButton.TabIndex = 2;
            this._upButton.Text = "Move Up";
            this._upButton.Click += new System.EventHandler(this._upButton_Click);
            // 
            // _downButton
            // 
            this._downButton.AutoSize = true;
            this._downButton.Location = new System.Drawing.Point(252, 9);
            this._downButton.MinimumSize = new System.Drawing.Size(0, 30);
            this._downButton.Name = "_downButton";
            this._downButton.Size = new System.Drawing.Size(75, 30);
            this._downButton.TabIndex = 3;
            this._downButton.Text = "Move Down";
            this._downButton.Click += new System.EventHandler(this._downButton_Click);
            // 
            // _rightButtons
            // 
            this._rightButtons.AutoSize = true;
            this._rightButtons.Controls.Add(this._applyButton);
            this._rightButtons.Controls.Add(this.launchModButton);
            this._rightButtons.Controls.Add(this._repairButton);
            this._rightButtons.Controls.Add(this._createButton);
            this._rightButtons.Dock = System.Windows.Forms.DockStyle.Right;
            this._rightButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this._rightButtons.Location = new System.Drawing.Point(641, 0);
            this._rightButtons.Name = "_rightButtons";
            this._rightButtons.Padding = new System.Windows.Forms.Padding(0, 6, 6, 6);
            this._rightButtons.Size = new System.Drawing.Size(383, 46);
            this._rightButtons.TabIndex = 1;
            this._rightButtons.WrapContents = false;
            // 
            // _applyButton
            // 
            this._applyButton.AutoSize = true;
            this._applyButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
            this._applyButton.Location = new System.Drawing.Point(264, 9);
            this._applyButton.MinimumSize = new System.Drawing.Size(110, 30);
            this._applyButton.Name = "_applyButton";
            this._applyButton.Size = new System.Drawing.Size(110, 30);
            this._applyButton.TabIndex = 0;
            this._applyButton.Text = "Apply";
            this._applyButton.Click += new System.EventHandler(this._applyButton_Click);
            // 
            // launchModButton
            // 
            this.launchModButton.AutoSize = true;
            this.launchModButton.Enabled = false;
            this.launchModButton.Location = new System.Drawing.Point(182, 9);
            this.launchModButton.MinimumSize = new System.Drawing.Size(0, 30);
            this.launchModButton.Name = "launchModButton";
            this.launchModButton.Size = new System.Drawing.Size(76, 30);
            this.launchModButton.TabIndex = 1;
            this.launchModButton.Text = "Launch Mod";
            this._launchTips.SetToolTip(this.launchModButton, "Start the game for the selected mod: into the level it starts in (as its author set it, or its only level), with that level's own UI (radial menu, HUD... - only while it's played) and the launch options the mod asks for. Your other Launch Game settings apply as usual.");
            this.launchModButton.Click += new System.EventHandler(this.launchModButton_Click);
            // 
            // _repairButton
            // 
            this._repairButton.AutoSize = true;
            this._repairButton.Location = new System.Drawing.Point(101, 9);
            this._repairButton.MinimumSize = new System.Drawing.Size(0, 30);
            this._repairButton.Name = "_repairButton";
            this._repairButton.Size = new System.Drawing.Size(75, 30);
            this._repairButton.TabIndex = 2;
            this._repairButton.Text = "Repair";
            this._repairButton.Click += new System.EventHandler(this._repairButton_Click);
            // 
            // _createButton
            // 
            this._createButton.AutoSize = true;
            this._createButton.Location = new System.Drawing.Point(3, 9);
            this._createButton.MinimumSize = new System.Drawing.Size(0, 30);
            this._createButton.Name = "_createButton";
            this._createButton.Size = new System.Drawing.Size(90, 30);
            this._createButton.TabIndex = 3;
            this._createButton.Text = "Create a Mod...";
            this._createButton.Click += new System.EventHandler(this._createButton_Click);
            // 
            // _copiesNoteTip
            // 
            this._copiesNoteTip.AutoPopDelay = 30000;
            // 
            // _launchTips
            // 
            this._launchTips.AutoPopDelay = 15000;
            // 
            // ModManagerForm
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1024, 621);
            this.Controls.Add(this._split);
            this.Controls.Add(this._statusLabel);
            this.Controls.Add(this._buttonBar);
            this.Controls.Add(this._header);
            this.MinimumSize = new System.Drawing.Size(820, 500);
            this.Name = "ModManagerForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Mod Manager";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.ModManagerForm_FormClosed);
            this.Shown += new System.EventHandler(this.OnFirstShown);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.OnDragDrop);
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.OnDragEnter);
            this._header.ResumeLayout(false);
            this._stateRow.ResumeLayout(false);
            this._stateRow.PerformLayout();
            this._split.Panel1.ResumeLayout(false);
            this._split.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._split)).EndInit();
            this._split.ResumeLayout(false);
            this._listPanel.ResumeLayout(false);
            this._detailPanel.ResumeLayout(false);
            this._detailPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._preview)).EndInit();
            this._buttonBar.ResumeLayout(false);
            this._buttonBar.PerformLayout();
            this._leftButtons.ResumeLayout(false);
            this._leftButtons.PerformLayout();
            this._rightButtons.ResumeLayout(false);
            this._rightButtons.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel _header;
        private System.Windows.Forms.Label _copiesNote;
        private System.Windows.Forms.FlowLayoutPanel _stateRow;
        private System.Windows.Forms.Label _installState;
        private System.Windows.Forms.LinkLabel _installAction;
        private System.Windows.Forms.LinkLabel _recheck;
        private System.Windows.Forms.Label _headline;
        private System.Windows.Forms.SplitContainer _split;
        private System.Windows.Forms.Panel _listPanel;
        private System.Windows.Forms.ListView modList;
        private System.Windows.Forms.ColumnHeader _modColumn;
        private System.Windows.Forms.ColumnHeader _versionColumn;
        private System.Windows.Forms.ColumnHeader _authorColumn;
        private System.Windows.Forms.ColumnHeader _typeColumn;
        private System.Windows.Forms.ColumnHeader _statusColumn;
        private System.Windows.Forms.Label _orderTip;
        private System.Windows.Forms.Panel _detailPanel;
        private System.Windows.Forms.TextBox _details;
        private System.Windows.Forms.Label _detailByline;
        private System.Windows.Forms.Label _detailTitle;
        private System.Windows.Forms.PictureBox _preview;
        private System.Windows.Forms.Label _statusLabel;
        private System.Windows.Forms.Panel _buttonBar;
        private System.Windows.Forms.FlowLayoutPanel _leftButtons;
        private System.Windows.Forms.Button _addButton;
        private System.Windows.Forms.Button _removeButton;
        private System.Windows.Forms.Button _upButton;
        private System.Windows.Forms.Button _downButton;
        private System.Windows.Forms.FlowLayoutPanel _rightButtons;
        private System.Windows.Forms.Button _applyButton;
        private System.Windows.Forms.Button launchModButton;
        private System.Windows.Forms.Button _repairButton;
        private System.Windows.Forms.Button _createButton;
        private System.Windows.Forms.ToolTip _copiesNoteTip;
        private System.Windows.Forms.ToolTip _launchTips;
    }
}
#endif

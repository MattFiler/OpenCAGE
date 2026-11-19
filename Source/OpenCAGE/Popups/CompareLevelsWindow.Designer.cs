namespace OpenCAGE.Popups
{
    partial class CompareLevelsWindow
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
            this._topPanel = new System.Windows.Forms.Panel();
            this._beforeLabel = new System.Windows.Forms.Label();
            this._before = new System.Windows.Forms.ComboBox();
            this._afterLabel = new System.Windows.Forms.Label();
            this._after = new System.Windows.Forms.ComboBox();
            this._categories = new System.Windows.Forms.CheckedListBox();
            this._compare = new System.Windows.Forms.Button();
            this._split = new System.Windows.Forms.SplitContainer();
            this._tree = new System.Windows.Forms.TreeView();
            this._details = new System.Windows.Forms.TextBox();
            this._buttonsPanel = new System.Windows.Forms.Panel();
            this._copy = new System.Windows.Forms.Button();
            this._save = new System.Windows.Forms.Button();
            this._goTo = new System.Windows.Forms.Button();
            this._status = new System.Windows.Forms.Label();
            this._topPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._split)).BeginInit();
            this._split.Panel1.SuspendLayout();
            this._split.Panel2.SuspendLayout();
            this._split.SuspendLayout();
            this._buttonsPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // _topPanel
            //
            this._topPanel.Controls.Add(this._beforeLabel);
            this._topPanel.Controls.Add(this._before);
            this._topPanel.Controls.Add(this._afterLabel);
            this._topPanel.Controls.Add(this._after);
            this._topPanel.Controls.Add(this._categories);
            this._topPanel.Controls.Add(this._compare);
            this._topPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this._topPanel.Location = new System.Drawing.Point(0, 0);
            this._topPanel.Name = "_topPanel";
            this._topPanel.Padding = new System.Windows.Forms.Padding(8);
            this._topPanel.Size = new System.Drawing.Size(1084, 104);
            this._topPanel.TabIndex = 1;
            //
            // _beforeLabel
            //
            this._beforeLabel.AutoSize = true;
            this._beforeLabel.Location = new System.Drawing.Point(8, 13);
            this._beforeLabel.Name = "_beforeLabel";
            this._beforeLabel.TabIndex = 0;
            this._beforeLabel.Text = "Before:";
            //
            // _before
            //
            this._before.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._before.Location = new System.Drawing.Point(64, 9);
            this._before.Name = "_before";
            this._before.Size = new System.Drawing.Size(430, 21);
            this._before.TabIndex = 1;
            this._before.SelectedIndexChanged += new System.EventHandler(this.SideChanged);
            //
            // _afterLabel
            //
            this._afterLabel.AutoSize = true;
            this._afterLabel.Location = new System.Drawing.Point(8, 43);
            this._afterLabel.Name = "_afterLabel";
            this._afterLabel.TabIndex = 2;
            this._afterLabel.Text = "After:";
            //
            // _after
            //
            this._after.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._after.Location = new System.Drawing.Point(64, 39);
            this._after.Name = "_after";
            this._after.Size = new System.Drawing.Size(430, 21);
            this._after.TabIndex = 3;
            this._after.SelectedIndexChanged += new System.EventHandler(this.SideChanged);
            //
            // _categories
            //
            this._categories.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this._categories.CheckOnClick = true;
            this._categories.ColumnWidth = 125;
            this._categories.Location = new System.Drawing.Point(510, 8);
            this._categories.MultiColumn = true;
            this._categories.Name = "_categories";
            this._categories.Size = new System.Drawing.Size(1264, 88);
            this._categories.TabIndex = 4;
            //
            // _compare
            //
            this._compare.AutoSize = true;
            this._compare.Location = new System.Drawing.Point(64, 70);
            this._compare.Name = "_compare";
            this._compare.TabIndex = 5;
            this._compare.Text = "Compare";
            this._compare.Click += new System.EventHandler(this._compare_Click);
            //
            // _split
            //
            this._split.Dock = System.Windows.Forms.DockStyle.Fill;
            this._split.Location = new System.Drawing.Point(0, 104);
            this._split.Name = "_split";
            this._split.Orientation = System.Windows.Forms.Orientation.Horizontal;
            //
            // _split.Panel1
            //
            this._split.Panel1.Controls.Add(this._tree);
            //
            // _split.Panel2
            //
            this._split.Panel2.Controls.Add(this._details);
            this._split.Panel2.Controls.Add(this._buttonsPanel);
            this._split.Panel2.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);
            this._split.Size = new System.Drawing.Size(1084, 500);
            this._split.SplitterDistance = 355;
            this._split.TabIndex = 0;
            //
            // _tree
            //
            this._tree.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tree.HideSelection = false;
            this._tree.Location = new System.Drawing.Point(0, 0);
            this._tree.Name = "_tree";
            this._tree.ShowNodeToolTips = true;
            this._tree.Size = new System.Drawing.Size(1084, 394);
            this._tree.TabIndex = 0;
            this._tree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this._tree_AfterSelect);
            this._tree.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this._tree_NodeMouseDoubleClick);
            //
            // _details
            //
            this._details.Dock = System.Windows.Forms.DockStyle.Fill;
            this._details.Font = new System.Drawing.Font("Courier New", 9F);
            this._details.Location = new System.Drawing.Point(8, 4);
            this._details.Multiline = true;
            this._details.Name = "_details";
            this._details.ReadOnly = true;
            this._details.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this._details.Size = new System.Drawing.Size(1068, 113);
            this._details.TabIndex = 0;
            this._details.WordWrap = false;
            //
            // _buttonsPanel
            //
            this._buttonsPanel.Controls.Add(this._copy);
            this._buttonsPanel.Controls.Add(this._save);
            this._buttonsPanel.Controls.Add(this._goTo);
            this._buttonsPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._buttonsPanel.Location = new System.Drawing.Point(8, 117);
            this._buttonsPanel.Name = "_buttonsPanel";
            this._buttonsPanel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this._buttonsPanel.Size = new System.Drawing.Size(1068, 36);
            this._buttonsPanel.TabIndex = 1;
            //
            // _copy
            //
            this._copy.AutoSize = true;
            this._copy.Enabled = false;
            this._copy.Location = new System.Drawing.Point(0, 6);
            this._copy.Name = "_copy";
            this._copy.TabIndex = 0;
            this._copy.Text = "Copy Report";
            this._copy.Click += new System.EventHandler(this._copy_Click);
            //
            // _save
            //
            this._save.AutoSize = true;
            this._save.Enabled = false;
            this._save.Location = new System.Drawing.Point(110, 6);
            this._save.Name = "_save";
            this._save.TabIndex = 1;
            this._save.Text = "Save Report...";
            this._save.Click += new System.EventHandler(this._save_Click);
            //
            // _goTo
            //
            this._goTo.AutoSize = true;
            this._goTo.Enabled = false;
            this._goTo.Location = new System.Drawing.Point(230, 6);
            this._goTo.Name = "_goTo";
            this._goTo.TabIndex = 2;
            this._goTo.Text = "Go To";
            this._goTo.Click += new System.EventHandler(this._goTo_Click);
            //
            // _status
            //
            this._status.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._status.Location = new System.Drawing.Point(0, 659);
            this._status.Name = "_status";
            this._status.Padding = new System.Windows.Forms.Padding(8, 4, 8, 0);
            this._status.Size = new System.Drawing.Size(1084, 22);
            this._status.TabIndex = 2;
            this._status.Text = "Pick two copies of a level and Compare. Loading each takes a few seconds (big levels longer).";
            //
            // CompareLevelsWindow
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1084, 681);
            this.Controls.Add(this._split);
            this.Controls.Add(this._topPanel);
            this.Controls.Add(this._status);
            this.MinimumSize = new System.Drawing.Size(760, 480);
            this.Name = "CompareLevelsWindow";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Compare Levels";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.CompareLevelsWindow_FormClosed);
            this._topPanel.ResumeLayout(false);
            this._topPanel.PerformLayout();
            this._split.Panel1.ResumeLayout(false);
            this._split.Panel2.ResumeLayout(false);
            this._split.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._split)).EndInit();
            this._split.ResumeLayout(false);
            this._buttonsPanel.ResumeLayout(false);
            this._buttonsPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel _topPanel;
        private System.Windows.Forms.Label _beforeLabel;
        private System.Windows.Forms.ComboBox _before;
        private System.Windows.Forms.Label _afterLabel;
        private System.Windows.Forms.ComboBox _after;
        private System.Windows.Forms.CheckedListBox _categories;
        private System.Windows.Forms.Button _compare;
        private System.Windows.Forms.SplitContainer _split;
        private System.Windows.Forms.TreeView _tree;
        private System.Windows.Forms.TextBox _details;
        private System.Windows.Forms.Panel _buttonsPanel;
        private System.Windows.Forms.Button _copy;
        private System.Windows.Forms.Button _save;
        private System.Windows.Forms.Button _goTo;
        private System.Windows.Forms.Label _status;
    }
}

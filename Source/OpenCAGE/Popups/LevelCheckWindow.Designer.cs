namespace OpenCAGE.Popups
{
    partial class LevelCheckWindow
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
            this._run = new System.Windows.Forms.Button();
            this._showDismissed = new System.Windows.Forms.CheckBox();
            this._summary = new System.Windows.Forms.Label();
            this._split = new System.Windows.Forms.SplitContainer();
            this._list = new System.Windows.Forms.ListView();
            this._colSeverity = new System.Windows.Forms.ColumnHeader();
            this._colFinding = new System.Windows.Forms.ColumnHeader();
            this._colFix = new System.Windows.Forms.ColumnHeader();
            this._details = new System.Windows.Forms.TextBox();
            this._buttonsPanel = new System.Windows.Forms.Panel();
            this._fix = new System.Windows.Forms.Button();
            this._dismiss = new System.Windows.Forms.Button();
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
            this._topPanel.Controls.Add(this._run);
            this._topPanel.Controls.Add(this._showDismissed);
            this._topPanel.Controls.Add(this._summary);
            this._topPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this._topPanel.Location = new System.Drawing.Point(0, 0);
            this._topPanel.Name = "_topPanel";
            this._topPanel.Padding = new System.Windows.Forms.Padding(8, 8, 8, 4);
            this._topPanel.Size = new System.Drawing.Size(984, 58);
            this._topPanel.TabIndex = 1;
            //
            // _run
            //
            this._run.AutoSize = true;
            this._run.Location = new System.Drawing.Point(8, 8);
            this._run.Name = "_run";
            this._run.TabIndex = 0;
            this._run.Text = "Check Now";
            this._run.Click += new System.EventHandler(this._run_Click);
            //
            // _showDismissed
            //
            this._showDismissed.AutoSize = true;
            this._showDismissed.Location = new System.Drawing.Point(110, 12);
            this._showDismissed.Name = "_showDismissed";
            this._showDismissed.TabIndex = 1;
            this._showDismissed.Text = "Show dismissed";
            this._showDismissed.CheckedChanged += new System.EventHandler(this._showDismissed_CheckedChanged);
            //
            // _summary
            //
            this._summary.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this._summary.Location = new System.Drawing.Point(8, 36);
            this._summary.Name = "_summary";
            this._summary.Size = new System.Drawing.Size(1744, 18);
            this._summary.TabIndex = 2;
            //
            // _split
            //
            this._split.Dock = System.Windows.Forms.DockStyle.Fill;
            this._split.Location = new System.Drawing.Point(0, 58);
            this._split.Name = "_split";
            this._split.Orientation = System.Windows.Forms.Orientation.Horizontal;
            //
            // _split.Panel1
            //
            this._split.Panel1.Controls.Add(this._list);
            //
            // _split.Panel2
            //
            this._split.Panel2.Controls.Add(this._details);
            this._split.Panel2.Controls.Add(this._buttonsPanel);
            this._split.Panel2.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);
            this._split.Size = new System.Drawing.Size(984, 500);
            this._split.SplitterDistance = 355;
            this._split.TabIndex = 0;
            //
            // _list
            //
            this._list.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colSeverity,
            this._colFinding,
            this._colFix});
            this._list.Dock = System.Windows.Forms.DockStyle.Fill;
            this._list.FullRowSelect = true;
            this._list.HideSelection = false;
            this._list.Location = new System.Drawing.Point(0, 0);
            this._list.MultiSelect = false;
            this._list.Name = "_list";
            this._list.Size = new System.Drawing.Size(984, 369);
            this._list.TabIndex = 0;
            this._list.View = System.Windows.Forms.View.Details;
            this._list.SelectedIndexChanged += new System.EventHandler(this._list_SelectedIndexChanged);
            this._list.DoubleClick += new System.EventHandler(this._list_DoubleClick);
            //
            // _colSeverity
            //
            this._colSeverity.Text = "";
            this._colSeverity.Width = 90;
            //
            // _colFinding
            //
            this._colFinding.Text = "Finding";
            this._colFinding.Width = 760;
            //
            // _colFix
            //
            this._colFix.Text = "Fix";
            this._colFix.Width = 110;
            //
            // _details
            //
            this._details.Dock = System.Windows.Forms.DockStyle.Fill;
            this._details.Location = new System.Drawing.Point(8, 4);
            this._details.Multiline = true;
            this._details.Name = "_details";
            this._details.ReadOnly = true;
            this._details.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._details.Size = new System.Drawing.Size(968, 104);
            this._details.TabIndex = 0;
            //
            // _buttonsPanel
            //
            this._buttonsPanel.Controls.Add(this._fix);
            this._buttonsPanel.Controls.Add(this._dismiss);
            this._buttonsPanel.Controls.Add(this._goTo);
            this._buttonsPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._buttonsPanel.Location = new System.Drawing.Point(8, 108);
            this._buttonsPanel.Name = "_buttonsPanel";
            this._buttonsPanel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this._buttonsPanel.Size = new System.Drawing.Size(968, 36);
            this._buttonsPanel.TabIndex = 1;
            this._buttonsPanel.Resize += new System.EventHandler(this._buttonsPanel_Resize);
            //
            // _fix
            //
            this._fix.AutoSize = true;
            this._fix.Enabled = false;
            this._fix.Location = new System.Drawing.Point(0, 6);
            this._fix.Name = "_fix";
            this._fix.TabIndex = 0;
            this._fix.Text = "Fix";
            this._fix.Click += new System.EventHandler(this._fix_Click);
            //
            // _dismiss
            //
            this._dismiss.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this._dismiss.AutoSize = true;
            this._dismiss.Enabled = false;
            this._dismiss.Location = new System.Drawing.Point(812, 6);
            this._dismiss.Name = "_dismiss";
            this._dismiss.TabIndex = 1;
            this._dismiss.Text = "Dismiss";
            this._dismiss.Click += new System.EventHandler(this._dismiss_Click);
            //
            // _goTo
            //
            this._goTo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this._goTo.AutoSize = true;
            this._goTo.Enabled = false;
            this._goTo.Location = new System.Drawing.Point(893, 6);
            this._goTo.Name = "_goTo";
            this._goTo.TabIndex = 2;
            this._goTo.Text = "Go To";
            this._goTo.Click += new System.EventHandler(this._goTo_Click);
            //
            // _status
            //
            this._status.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._status.Location = new System.Drawing.Point(0, 579);
            this._status.Name = "_status";
            this._status.Padding = new System.Windows.Forms.Padding(8, 4, 8, 0);
            this._status.Size = new System.Drawing.Size(984, 22);
            this._status.TabIndex = 2;
            this._status.Text = "Fixes are ordinary edits (Undo puts them back); Save & Build takes them into the game.";
            //
            // LevelCheckWindow
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(984, 601);
            this.Controls.Add(this._split);
            this.Controls.Add(this._topPanel);
            this.Controls.Add(this._status);
            this.MinimumSize = new System.Drawing.Size(720, 420);
            this.Name = "LevelCheckWindow";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Level Check";
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
        private System.Windows.Forms.Button _run;
        private System.Windows.Forms.CheckBox _showDismissed;
        private System.Windows.Forms.Label _summary;
        private System.Windows.Forms.SplitContainer _split;
        private System.Windows.Forms.ListView _list;
        private System.Windows.Forms.ColumnHeader _colSeverity;
        private System.Windows.Forms.ColumnHeader _colFinding;
        private System.Windows.Forms.ColumnHeader _colFix;
        private System.Windows.Forms.TextBox _details;
        private System.Windows.Forms.Panel _buttonsPanel;
        private System.Windows.Forms.Button _fix;
        private System.Windows.Forms.Button _dismiss;
        private System.Windows.Forms.Button _goTo;
        private System.Windows.Forms.Label _status;
    }
}

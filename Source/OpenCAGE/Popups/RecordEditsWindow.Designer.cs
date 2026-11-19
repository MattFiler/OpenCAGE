namespace OpenCAGE.Popups
{
    partial class RecordEditsWindow
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
            this._recording = new System.Windows.Forms.GroupBox();
            this._steps = new System.Windows.Forms.ListBox();
            this._hint = new System.Windows.Forms.Label();
            this._recordRow = new System.Windows.Forms.Panel();
            this._record = new System.Windows.Forms.Button();
            this._stop = new System.Windows.Forms.Button();
            this._discard = new System.Windows.Forms.Button();
            this._state = new System.Windows.Forms.Label();
            this._replay = new System.Windows.Forms.GroupBox();
            this._plan = new System.Windows.Forms.ListView();
            this._colNumber = new System.Windows.Forms.ColumnHeader();
            this._colStatus = new System.Windows.Forms.ColumnHeader();
            this._colStep = new System.Windows.Forms.ColumnHeader();
            this._colNote = new System.Windows.Forms.ColumnHeader();
            this._planSummary = new System.Windows.Forms.Label();
            this._replayRow = new System.Windows.Forms.Panel();
            this._openRecipe = new System.Windows.Forms.Button();
            this._idsLabel = new System.Windows.Forms.Label();
            this._ids = new System.Windows.Forms.ComboBox();
            this._apply = new System.Windows.Forms.Button();
            this._applyAll = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this._split)).BeginInit();
            this._split.Panel1.SuspendLayout();
            this._split.Panel2.SuspendLayout();
            this._split.SuspendLayout();
            this._recording.SuspendLayout();
            this._recordRow.SuspendLayout();
            this._replay.SuspendLayout();
            this._replayRow.SuspendLayout();
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
            this._split.Panel1.Controls.Add(this._recording);
            this._split.Panel1.Padding = new System.Windows.Forms.Padding(8, 8, 8, 4);
            //
            // _split.Panel2
            //
            this._split.Panel2.Controls.Add(this._replay);
            this._split.Panel2.Padding = new System.Windows.Forms.Padding(8, 4, 8, 8);
            this._split.Size = new System.Drawing.Size(964, 661);
            this._split.SplitterDistance = 469;
            this._split.TabIndex = 0;
            //
            // _recording
            //
            this._recording.Controls.Add(this._steps);
            this._recording.Controls.Add(this._hint);
            this._recording.Controls.Add(this._recordRow);
            this._recording.Dock = System.Windows.Forms.DockStyle.Fill;
            this._recording.Location = new System.Drawing.Point(8, 8);
            this._recording.Name = "_recording";
            this._recording.Padding = new System.Windows.Forms.Padding(8);
            this._recording.Size = new System.Drawing.Size(948, 457);
            this._recording.TabIndex = 0;
            this._recording.TabStop = false;
            this._recording.Text = "Record";
            //
            // _steps
            //
            this._steps.Dock = System.Windows.Forms.DockStyle.Fill;
            this._steps.IntegralHeight = false;
            this._steps.Location = new System.Drawing.Point(8, 55);
            this._steps.Name = "_steps";
            this._steps.Size = new System.Drawing.Size(932, 360);
            this._steps.TabIndex = 0;
            //
            // _hint
            //
            this._hint.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._hint.ForeColor = System.Drawing.SystemColors.GrayText;
            this._hint.Location = new System.Drawing.Point(8, 415);
            this._hint.Name = "_hint";
            this._hint.Size = new System.Drawing.Size(932, 34);
            this._hint.TabIndex = 1;
            this._hint.Text = "Every edit counts - the editor\'s windows, the viewport, AI tools - and what you undo while recording is left out. The recipe holds the net change, with what each thing was before, so a replay can tell when the target differs.";
            //
            // _recordRow
            //
            this._recordRow.Controls.Add(this._record);
            this._recordRow.Controls.Add(this._stop);
            this._recordRow.Controls.Add(this._discard);
            this._recordRow.Controls.Add(this._state);
            this._recordRow.Dock = System.Windows.Forms.DockStyle.Top;
            this._recordRow.Location = new System.Drawing.Point(8, 21);
            this._recordRow.Name = "_recordRow";
            this._recordRow.Size = new System.Drawing.Size(932, 34);
            this._recordRow.TabIndex = 2;
            //
            // _record
            //
            this._record.AutoSize = true;
            this._record.Location = new System.Drawing.Point(0, 4);
            this._record.Name = "_record";
            this._record.TabIndex = 0;
            this._record.Text = "Record";
            this._record.Click += new System.EventHandler(this._record_Click);
            //
            // _stop
            //
            this._stop.AutoSize = true;
            this._stop.Location = new System.Drawing.Point(90, 4);
            this._stop.Name = "_stop";
            this._stop.TabIndex = 1;
            this._stop.Text = "Stop && Save Recipe...";
            this._stop.Click += new System.EventHandler(this._stop_Click);
            //
            // _discard
            //
            this._discard.AutoSize = true;
            this._discard.Location = new System.Drawing.Point(250, 4);
            this._discard.Name = "_discard";
            this._discard.TabIndex = 2;
            this._discard.Text = "Discard";
            this._discard.Click += new System.EventHandler(this._discard_Click);
            //
            // _state
            //
            this._state.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this._state.Location = new System.Drawing.Point(340, 9);
            this._state.Name = "_state";
            this._state.Size = new System.Drawing.Size(1312, 20);
            this._state.TabIndex = 3;
            //
            // _replay
            //
            this._replay.Controls.Add(this._plan);
            this._replay.Controls.Add(this._planSummary);
            this._replay.Controls.Add(this._replayRow);
            this._replay.Dock = System.Windows.Forms.DockStyle.Fill;
            this._replay.Location = new System.Drawing.Point(8, 4);
            this._replay.Name = "_replay";
            this._replay.Padding = new System.Windows.Forms.Padding(8);
            this._replay.Size = new System.Drawing.Size(948, 176);
            this._replay.TabIndex = 0;
            this._replay.TabStop = false;
            this._replay.Text = "Replay";
            //
            // _plan
            //
            this._plan.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colNumber,
            this._colStatus,
            this._colStep,
            this._colNote});
            this._plan.Dock = System.Windows.Forms.DockStyle.Fill;
            this._plan.FullRowSelect = true;
            this._plan.HideSelection = false;
            this._plan.Location = new System.Drawing.Point(8, 95);
            this._plan.Name = "_plan";
            this._plan.Size = new System.Drawing.Size(932, 73);
            this._plan.TabIndex = 0;
            this._plan.View = System.Windows.Forms.View.Details;
            //
            // _colNumber
            //
            this._colNumber.Text = "#";
            this._colNumber.Width = 44;
            //
            // _colStatus
            //
            this._colStatus.Text = "Status";
            this._colStatus.Width = 100;
            //
            // _colStep
            //
            this._colStep.Text = "Step";
            this._colStep.Width = 520;
            //
            // _colNote
            //
            this._colNote.Text = "Note";
            this._colNote.Width = 260;
            //
            // _planSummary
            //
            this._planSummary.Dock = System.Windows.Forms.DockStyle.Top;
            this._planSummary.Location = new System.Drawing.Point(8, 55);
            this._planSummary.Name = "_planSummary";
            this._planSummary.Size = new System.Drawing.Size(932, 40);
            this._planSummary.TabIndex = 1;
            this._planSummary.Text = "Open a recipe to see what it would do here (nothing changes until Apply).";
            //
            // _replayRow
            //
            this._replayRow.Controls.Add(this._openRecipe);
            this._replayRow.Controls.Add(this._idsLabel);
            this._replayRow.Controls.Add(this._ids);
            this._replayRow.Controls.Add(this._apply);
            this._replayRow.Controls.Add(this._applyAll);
            this._replayRow.Dock = System.Windows.Forms.DockStyle.Top;
            this._replayRow.Location = new System.Drawing.Point(8, 21);
            this._replayRow.Name = "_replayRow";
            this._replayRow.Size = new System.Drawing.Size(932, 34);
            this._replayRow.TabIndex = 2;
            //
            // _openRecipe
            //
            this._openRecipe.AutoSize = true;
            this._openRecipe.Location = new System.Drawing.Point(0, 4);
            this._openRecipe.Name = "_openRecipe";
            this._openRecipe.TabIndex = 0;
            this._openRecipe.Text = "Open Recipe...";
            this._openRecipe.Click += new System.EventHandler(this._openRecipe_Click);
            //
            // _idsLabel
            //
            this._idsLabel.AutoSize = true;
            this._idsLabel.Location = new System.Drawing.Point(120, 9);
            this._idsLabel.Name = "_idsLabel";
            this._idsLabel.TabIndex = 1;
            this._idsLabel.Text = "Ids:";
            //
            // _ids
            //
            this._ids.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._ids.Items.AddRange(new object[] {
            "auto (keep on a copy, else new)",
            "keep the recorded ids",
            "new ids (remap)"});
            this._ids.Location = new System.Drawing.Point(150, 5);
            this._ids.Name = "_ids";
            this._ids.Size = new System.Drawing.Size(230, 21);
            this._ids.TabIndex = 2;
            //
            // _apply
            //
            this._apply.AutoSize = true;
            this._apply.Enabled = false;
            this._apply.Location = new System.Drawing.Point(400, 4);
            this._apply.Name = "_apply";
            this._apply.TabIndex = 3;
            this._apply.Text = "Apply";
            this._apply.Click += new System.EventHandler(this._apply_Click);
            //
            // _applyAll
            //
            this._applyAll.AutoSize = true;
            this._applyAll.Enabled = false;
            this._applyAll.Location = new System.Drawing.Point(490, 4);
            this._applyAll.Name = "_applyAll";
            this._applyAll.TabIndex = 4;
            this._applyAll.Text = "Apply, Overwriting Conflicts";
            this._applyAll.Click += new System.EventHandler(this._applyAll_Click);
            //
            // RecordEditsWindow
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(964, 661);
            this.Controls.Add(this._split);
            this.MinimumSize = new System.Drawing.Size(700, 480);
            this.Name = "RecordEditsWindow";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Record Edits";
            this.Load += new System.EventHandler(this.RecordEditsWindow_Load);
            this._split.Panel1.ResumeLayout(false);
            this._split.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._split)).EndInit();
            this._split.ResumeLayout(false);
            this._recording.ResumeLayout(false);
            this._recordRow.ResumeLayout(false);
            this._recordRow.PerformLayout();
            this._replay.ResumeLayout(false);
            this._replayRow.ResumeLayout(false);
            this._replayRow.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer _split;
        private System.Windows.Forms.GroupBox _recording;
        private System.Windows.Forms.ListBox _steps;
        private System.Windows.Forms.Label _hint;
        private System.Windows.Forms.Panel _recordRow;
        private System.Windows.Forms.Button _record;
        private System.Windows.Forms.Button _stop;
        private System.Windows.Forms.Button _discard;
        private System.Windows.Forms.Label _state;
        private System.Windows.Forms.GroupBox _replay;
        private System.Windows.Forms.ListView _plan;
        private System.Windows.Forms.ColumnHeader _colNumber;
        private System.Windows.Forms.ColumnHeader _colStatus;
        private System.Windows.Forms.ColumnHeader _colStep;
        private System.Windows.Forms.ColumnHeader _colNote;
        private System.Windows.Forms.Label _planSummary;
        private System.Windows.Forms.Panel _replayRow;
        private System.Windows.Forms.Button _openRecipe;
        private System.Windows.Forms.Label _idsLabel;
        private System.Windows.Forms.ComboBox _ids;
        private System.Windows.Forms.Button _apply;
        private System.Windows.Forms.Button _applyAll;
    }
}

namespace OpenCAGE
{
    partial class LevelUiOverridesEditor
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
            this.list = new System.Windows.Forms.ListView();
            this.movieColumn = new System.Windows.Forms.ColumnHeader();
            this.fileColumn = new System.Windows.Forms.ColumnHeader();
            this.stateColumn = new System.Windows.Forms.ColumnHeader();
            this.buttons = new System.Windows.Forms.FlowLayoutPanel();
            this.closeBtn = new System.Windows.Forms.Button();
            this.removeBtn = new System.Windows.Forms.Button();
            this.exportBtn = new System.Windows.Forms.Button();
            this.exportGameBtn = new System.Windows.Forms.Button();
            this.importBtn = new System.Windows.Forms.Button();
            this.folderBtn = new System.Windows.Forms.Button();
            this.summary = new System.Windows.Forms.Label();
            this.tips = new System.Windows.Forms.ToolTip(this.components);
            this.buttons.SuspendLayout();
            this.SuspendLayout();
            //
            // list
            //
            this.list.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.movieColumn,
            this.fileColumn,
            this.stateColumn});
            this.list.Dock = System.Windows.Forms.DockStyle.Fill;
            this.list.FullRowSelect = true;
            this.list.HideSelection = false;
            this.list.Location = new System.Drawing.Point(0, 66);
            this.list.MultiSelect = false;
            this.list.Name = "list";
            this.list.ShowItemToolTips = true;
            this.list.Size = new System.Drawing.Size(860, 416);
            this.list.TabIndex = 0;
            this.list.View = System.Windows.Forms.View.Details;
            this.list.SelectedIndexChanged += new System.EventHandler(this.list_SelectedIndexChanged);
            this.list.DoubleClick += new System.EventHandler(this.list_DoubleClick);
            this.list.KeyDown += new System.Windows.Forms.KeyEventHandler(this.list_KeyDown);
            //
            // movieColumn
            //
            this.movieColumn.Text = "Movie";
            this.movieColumn.Width = 280;
            //
            // fileColumn
            //
            this.fileColumn.Text = "File";
            this.fileColumn.Width = 230;
            //
            // stateColumn
            //
            this.stateColumn.Text = "On this level";
            this.stateColumn.Width = 320;
            //
            // buttons
            //
            this.buttons.Controls.Add(this.closeBtn);
            this.buttons.Controls.Add(this.removeBtn);
            this.buttons.Controls.Add(this.exportBtn);
            this.buttons.Controls.Add(this.exportGameBtn);
            this.buttons.Controls.Add(this.importBtn);
            this.buttons.Controls.Add(this.folderBtn);
            this.buttons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.buttons.Location = new System.Drawing.Point(0, 482);
            this.buttons.Name = "buttons";
            this.buttons.Padding = new System.Windows.Forms.Padding(4);
            this.buttons.Size = new System.Drawing.Size(860, 38);
            this.buttons.TabIndex = 1;
            //
            // closeBtn
            //
            this.closeBtn.Location = new System.Drawing.Point(759, 7);
            this.closeBtn.Name = "closeBtn";
            this.closeBtn.Size = new System.Drawing.Size(90, 26);
            this.closeBtn.TabIndex = 0;
            this.closeBtn.Text = "Close";
            this.closeBtn.UseVisualStyleBackColor = true;
            this.closeBtn.Click += new System.EventHandler(this.closeBtn_Click);
            //
            // removeBtn
            //
            this.removeBtn.Location = new System.Drawing.Point(663, 7);
            this.removeBtn.Name = "removeBtn";
            this.removeBtn.Size = new System.Drawing.Size(90, 26);
            this.removeBtn.TabIndex = 1;
            this.removeBtn.Text = "Remove";
            this.tips.SetToolTip(this.removeBtn, "Take the level's own version of the selected movie out: the game's is used again.");
            this.removeBtn.UseVisualStyleBackColor = true;
            this.removeBtn.Click += new System.EventHandler(this.removeBtn_Click);
            //
            // exportBtn
            //
            this.exportBtn.Location = new System.Drawing.Point(567, 7);
            this.exportBtn.Name = "exportBtn";
            this.exportBtn.Size = new System.Drawing.Size(90, 26);
            this.exportBtn.TabIndex = 2;
            this.exportBtn.Text = "Export...";
            this.tips.SetToolTip(this.exportBtn, "Save the level's own version of the selected movie.");
            this.exportBtn.UseVisualStyleBackColor = true;
            this.exportBtn.Click += new System.EventHandler(this.exportBtn_Click);
            //
            // exportGameBtn
            //
            this.exportGameBtn.Location = new System.Drawing.Point(446, 7);
            this.exportGameBtn.Name = "exportGameBtn";
            this.exportGameBtn.Size = new System.Drawing.Size(115, 26);
            this.exportGameBtn.TabIndex = 3;
            this.exportGameBtn.Text = "Export Game\'s...";
            this.tips.SetToolTip(this.exportGameBtn, "Save the game\'s own version of the selected movie, from UI.PAK - a starting point for the level\'s own.");
            this.exportGameBtn.UseVisualStyleBackColor = true;
            this.exportGameBtn.Click += new System.EventHandler(this.exportGameBtn_Click);
            //
            // importBtn
            //
            this.importBtn.Location = new System.Drawing.Point(350, 7);
            this.importBtn.Name = "importBtn";
            this.importBtn.Size = new System.Drawing.Size(90, 26);
            this.importBtn.TabIndex = 4;
            this.importBtn.Text = "Import...";
            this.tips.SetToolTip(this.importBtn, "Give the level its own version of the selected movie: a .gfx (or the .swf it is made from), copied into the level\'s UI folder. Used from the next launch into the level.");
            this.importBtn.UseVisualStyleBackColor = true;
            this.importBtn.Click += new System.EventHandler(this.importBtn_Click);
            //
            // folderBtn
            //
            this.folderBtn.Location = new System.Drawing.Point(244, 7);
            this.folderBtn.Name = "folderBtn";
            this.folderBtn.Size = new System.Drawing.Size(100, 26);
            this.folderBtn.TabIndex = 5;
            this.folderBtn.Text = "Open Folder";
            this.tips.SetToolTip(this.folderBtn, "Open the level\'s UI folder (DATA\\ENV\\<level>\\UI), where its movies are kept under the game\'s file names.");
            this.folderBtn.UseVisualStyleBackColor = true;
            this.folderBtn.Click += new System.EventHandler(this.folderBtn_Click);
            //
            // summary
            //
            this.summary.Dock = System.Windows.Forms.DockStyle.Top;
            this.summary.Location = new System.Drawing.Point(0, 0);
            this.summary.Name = "summary";
            this.summary.Padding = new System.Windows.Forms.Padding(6, 6, 6, 0);
            this.summary.Size = new System.Drawing.Size(860, 66);
            this.summary.TabIndex = 2;
            //
            // tips
            //
            this.tips.AutoPopDelay = 15000;
            //
            // LevelUiOverridesEditor
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.closeBtn;
            this.ClientSize = new System.Drawing.Size(860, 520);
            this.Controls.Add(this.list);
            this.Controls.Add(this.buttons);
            this.Controls.Add(this.summary);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(560, 300);
            this.Name = "LevelUiOverridesEditor";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Level UI Overrides";
            this.Activated += new System.EventHandler(this.LevelUiOverridesEditor_Activated);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.LevelUiOverridesEditor_FormClosed);
            this.Load += new System.EventHandler(this.LevelUiOverridesEditor_Load);
            this.buttons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView list;
        private System.Windows.Forms.ColumnHeader movieColumn;
        private System.Windows.Forms.ColumnHeader fileColumn;
        private System.Windows.Forms.ColumnHeader stateColumn;
        private System.Windows.Forms.FlowLayoutPanel buttons;
        private System.Windows.Forms.Button closeBtn;
        private System.Windows.Forms.Button removeBtn;
        private System.Windows.Forms.Button exportBtn;
        private System.Windows.Forms.Button exportGameBtn;
        private System.Windows.Forms.Button importBtn;
        private System.Windows.Forms.Button folderBtn;
        private System.Windows.Forms.Label summary;
        private System.Windows.Forms.ToolTip tips;
    }
}

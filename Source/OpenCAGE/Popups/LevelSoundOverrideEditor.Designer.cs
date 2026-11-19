namespace OpenCAGE
{
    partial class LevelSoundOverrideEditor
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
            this.nameColumn = new System.Windows.Forms.ColumnHeader();
            this.fileColumn = new System.Windows.Forms.ColumnHeader();
            this.kindColumn = new System.Windows.Forms.ColumnHeader();
            this.sizeColumn = new System.Windows.Forms.ColumnHeader();
            this.levelColumn = new System.Windows.Forms.ColumnHeader();
            this.buttons = new System.Windows.Forms.FlowLayoutPanel();
            this.closeBtn = new System.Windows.Forms.Button();
            this.removeBtn = new System.Windows.Forms.Button();
            this.exportBtn = new System.Windows.Forms.Button();
            this.addBtn = new System.Windows.Forms.Button();
            this.bankBtn = new System.Windows.Forms.Button();
            this.tuneBtn = new System.Windows.Forms.Button();
            this.importBtn = new System.Windows.Forms.Button();
            this.searchRow = new System.Windows.Forms.TableLayoutPanel();
            this.searchLabel = new System.Windows.Forms.Label();
            this.search = new System.Windows.Forms.TextBox();
            this.summary = new System.Windows.Forms.Label();
            this.tips = new System.Windows.Forms.ToolTip(this.components);
            this.buttons.SuspendLayout();
            this.searchRow.SuspendLayout();
            this.SuspendLayout();
            //
            // list
            //
            this.list.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.nameColumn,
            this.fileColumn,
            this.kindColumn,
            this.sizeColumn,
            this.levelColumn});
            this.list.Dock = System.Windows.Forms.DockStyle.Fill;
            this.list.FullRowSelect = true;
            this.list.HideSelection = false;
            this.list.Location = new System.Drawing.Point(0, 100);
            this.list.Name = "list";
            this.list.ShowItemToolTips = true;
            this.list.Size = new System.Drawing.Size(1000, 322);
            this.list.TabIndex = 0;
            this.list.View = System.Windows.Forms.View.Details;
            this.list.SelectedIndexChanged += new System.EventHandler(this.list_SelectedIndexChanged);
            this.list.DoubleClick += new System.EventHandler(this.list_DoubleClick);
            this.list.KeyDown += new System.Windows.Forms.KeyEventHandler(this.list_KeyDown);
            //
            // nameColumn
            //
            this.nameColumn.Text = "Name";
            this.nameColumn.Width = 270;
            //
            // fileColumn
            //
            this.fileColumn.Text = "File";
            this.fileColumn.Width = 175;
            //
            // kindColumn
            //
            this.kindColumn.Text = "Kind";
            this.kindColumn.Width = 115;
            //
            // sizeColumn
            //
            this.sizeColumn.Text = "Size";
            this.sizeColumn.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.sizeColumn.Width = 65;
            //
            // levelColumn
            //
            this.levelColumn.Text = "On this level";
            this.levelColumn.Width = 350;
            //
            // buttons
            //
            this.buttons.Controls.Add(this.closeBtn);
            this.buttons.Controls.Add(this.removeBtn);
            this.buttons.Controls.Add(this.exportBtn);
            this.buttons.Controls.Add(this.addBtn);
            this.buttons.Controls.Add(this.bankBtn);
            this.buttons.Controls.Add(this.tuneBtn);
            this.buttons.Controls.Add(this.importBtn);
            this.buttons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.buttons.Location = new System.Drawing.Point(0, 422);
            this.buttons.Name = "buttons";
            this.buttons.Padding = new System.Windows.Forms.Padding(4);
            this.buttons.Size = new System.Drawing.Size(1000, 38);
            this.buttons.TabIndex = 1;
            //
            // closeBtn
            //
            this.closeBtn.Location = new System.Drawing.Point(899, 7);
            this.closeBtn.Name = "closeBtn";
            this.closeBtn.Size = new System.Drawing.Size(90, 26);
            this.closeBtn.TabIndex = 0;
            this.closeBtn.Text = "Close";
            this.closeBtn.UseVisualStyleBackColor = true;
            this.closeBtn.Click += new System.EventHandler(this.closeBtn_Click);
            //
            // removeBtn
            //
            this.removeBtn.Location = new System.Drawing.Point(803, 7);
            this.removeBtn.Name = "removeBtn";
            this.removeBtn.Size = new System.Drawing.Size(90, 26);
            this.removeBtn.TabIndex = 1;
            this.removeBtn.Text = "Remove";
            this.tips.SetToolTip(this.removeBtn, "Take the selected files out of the package (on this level the game reads the base game's instead, or nothing for a file only this level has), and put the base game's audio back in place of the selected sounds in soundbanks.");
            this.removeBtn.UseVisualStyleBackColor = true;
            this.removeBtn.Click += new System.EventHandler(this.removeBtn_Click);
            //
            // exportBtn
            //
            this.exportBtn.Location = new System.Drawing.Point(707, 7);
            this.exportBtn.Name = "exportBtn";
            this.exportBtn.Size = new System.Drawing.Size(90, 26);
            this.exportBtn.TabIndex = 2;
            this.exportBtn.Text = "Export...";
            this.tips.SetToolTip(this.exportBtn, "Save the selected soundbank or stream - or a sound from inside a soundbank - out of the package, as Wwise audio (.wem) or as a .wav.");
            this.exportBtn.UseVisualStyleBackColor = true;
            this.exportBtn.Click += new System.EventHandler(this.exportBtn_Click);
            //
            // addBtn
            //
            this.addBtn.Location = new System.Drawing.Point(601, 7);
            this.addBtn.Name = "addBtn";
            this.addBtn.Size = new System.Drawing.Size(100, 26);
            this.addBtn.TabIndex = 3;
            this.addBtn.Text = "Add Files...";
            this.tips.SetToolTip(this.addBtn, "Put a soundbank (.bnk, built with the game's Wwise version) or a stream (.wem named for its id) in the package as it is. One with the same id as the base game's is read instead of it on this level - except a permanent soundbank, which the game loads before any level.");
            this.addBtn.UseVisualStyleBackColor = true;
            this.addBtn.Click += new System.EventHandler(this.addBtn_Click);
            //
            // bankBtn
            //
            this.bankBtn.Location = new System.Drawing.Point(475, 7);
            this.bankBtn.Name = "bankBtn";
            this.bankBtn.Size = new System.Drawing.Size(120, 26);
            this.bankBtn.TabIndex = 4;
            this.bankBtn.Text = "New Soundbank...";
            this.tips.SetToolTip(this.bankBtn, "Make a soundbank of the level's own: new sound events, each playing a .wav and sounding like a sound of the game's you pick. With a soundbank of the level's own selected, add events to it or take them out.");
            this.bankBtn.UseVisualStyleBackColor = true;
            this.bankBtn.Click += new System.EventHandler(this.bankBtn_Click);
            //
            // tuneBtn
            //
            this.tuneBtn.Location = new System.Drawing.Point(364, 7);
            this.tuneBtn.Name = "tuneBtn";
            this.tuneBtn.Size = new System.Drawing.Size(105, 26);
            this.tuneBtn.TabIndex = 5;
            this.tuneBtn.Text = "Tune Event...";
            this.tips.SetToolTip(this.tuneBtn, "Change how a sound event plays on this level alone: volume, pitch, start delay, fades, what its stop reaches, looping and how far away it can be heard - in this package's copies of its soundbanks.");
            this.tuneBtn.UseVisualStyleBackColor = true;
            this.tuneBtn.Click += new System.EventHandler(this.tuneBtn_Click);
            //
            // importBtn
            //
            this.importBtn.Location = new System.Drawing.Point(248, 7);
            this.importBtn.Name = "importBtn";
            this.importBtn.Size = new System.Drawing.Size(110, 26);
            this.importBtn.TabIndex = 6;
            this.importBtn.Text = "Import WAV...";
            this.tips.SetToolTip(this.importBtn, "Make game audio from a .wav: pick the sound it replaces on this level (by the sound events that play it), or give it a stream id of its own. Encoded the way the game's own audio is - no Wwise needed.");
            this.importBtn.UseVisualStyleBackColor = true;
            this.importBtn.Click += new System.EventHandler(this.importBtn_Click);
            //
            // searchRow
            //
            this.searchRow.ColumnCount = 2;
            this.searchRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.searchRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.searchRow.Controls.Add(this.searchLabel, 0, 0);
            this.searchRow.Controls.Add(this.search, 1, 0);
            this.searchRow.Dock = System.Windows.Forms.DockStyle.Top;
            this.searchRow.Location = new System.Drawing.Point(0, 72);
            this.searchRow.Name = "searchRow";
            this.searchRow.Padding = new System.Windows.Forms.Padding(3, 0, 3, 2);
            this.searchRow.RowCount = 1;
            this.searchRow.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.searchRow.Size = new System.Drawing.Size(1000, 28);
            this.searchRow.TabIndex = 2;
            //
            // searchLabel
            //
            this.searchLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.searchLabel.AutoSize = true;
            this.searchLabel.Location = new System.Drawing.Point(6, 7);
            this.searchLabel.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.searchLabel.Name = "searchLabel";
            this.searchLabel.Size = new System.Drawing.Size(44, 13);
            this.searchLabel.TabIndex = 0;
            this.searchLabel.Text = "Search:";
            //
            // search
            //
            this.search.Dock = System.Windows.Forms.DockStyle.Fill;
            this.search.Location = new System.Drawing.Point(56, 3);
            this.search.Name = "search";
            this.search.Size = new System.Drawing.Size(938, 20);
            this.search.TabIndex = 1;
            this.search.TextChanged += new System.EventHandler(this.search_TextChanged);
            //
            // summary
            //
            this.summary.Dock = System.Windows.Forms.DockStyle.Top;
            this.summary.Location = new System.Drawing.Point(0, 0);
            this.summary.Name = "summary";
            this.summary.Padding = new System.Windows.Forms.Padding(6, 6, 6, 0);
            this.summary.Size = new System.Drawing.Size(1000, 72);
            this.summary.TabIndex = 3;
            //
            // LevelSoundOverrideEditor
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.closeBtn;
            this.ClientSize = new System.Drawing.Size(1000, 460);
            this.Controls.Add(this.list);
            this.Controls.Add(this.buttons);
            this.Controls.Add(this.searchRow);
            this.Controls.Add(this.summary);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(780, 300);
            this.Name = "LevelSoundOverrideEditor";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Level Sound Override";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.LevelSoundOverrideEditor_FormClosed);
            this.Load += new System.EventHandler(this.LevelSoundOverrideEditor_Load);
            this.buttons.ResumeLayout(false);
            this.searchRow.ResumeLayout(false);
            this.searchRow.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView list;
        private System.Windows.Forms.ColumnHeader nameColumn;
        private System.Windows.Forms.ColumnHeader fileColumn;
        private System.Windows.Forms.ColumnHeader kindColumn;
        private System.Windows.Forms.ColumnHeader sizeColumn;
        private System.Windows.Forms.ColumnHeader levelColumn;
        private System.Windows.Forms.FlowLayoutPanel buttons;
        private System.Windows.Forms.Button closeBtn;
        private System.Windows.Forms.Button removeBtn;
        private System.Windows.Forms.Button exportBtn;
        private System.Windows.Forms.Button addBtn;
        private System.Windows.Forms.Button bankBtn;
        private System.Windows.Forms.Button tuneBtn;
        private System.Windows.Forms.Button importBtn;
        private System.Windows.Forms.TableLayoutPanel searchRow;
        private System.Windows.Forms.Label searchLabel;
        private System.Windows.Forms.TextBox search;
        private System.Windows.Forms.Label summary;
        private System.Windows.Forms.ToolTip tips;
    }
}

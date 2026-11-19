namespace OpenCAGE
{
    partial class LevelLaunchSettingsEditor
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
            this.body = new System.Windows.Forms.TableLayoutPanel();
            this.textureHeading = new System.Windows.Forms.Label();
            this.heapRow = new System.Windows.Forms.FlowLayoutPanel();
            this.heapOn = new System.Windows.Forms.CheckBox();
            this.heapMB = new System.Windows.Forms.NumericUpDown();
            this.heapUnit = new System.Windows.Forms.Label();
            this.heapNote = new System.Windows.Forms.Label();
            this.collideHeading = new System.Windows.Forms.Label();
            this.npcCollide = new System.Windows.Forms.CheckBox();
            this.playerCollide = new System.Windows.Forms.CheckBox();
            this.collideNote = new System.Windows.Forms.Label();
            this.loadingHeading = new System.Windows.Forms.Label();
            this.playlistRow = new System.Windows.Forms.FlowLayoutPanel();
            this.playlistOn = new System.Windows.Forms.CheckBox();
            this.playlist = new System.Windows.Forms.ComboBox();
            this.moviesOn = new System.Windows.Forms.CheckBox();
            this.moviesRow = new System.Windows.Forms.FlowLayoutPanel();
            this.movies = new System.Windows.Forms.ListBox();
            this.movieButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.addMovieBtn = new System.Windows.Forms.Button();
            this.removeMovieBtn = new System.Windows.Forms.Button();
            this.moviesToEnd = new System.Windows.Forms.CheckBox();
            this.playlistNote = new System.Windows.Forms.Label();
            this.animHeading = new System.Windows.Forms.Label();
            this.animOn = new System.Windows.Forms.CheckBox();
            this.capHeading = new System.Windows.Forms.Label();
            this.capNote = new System.Windows.Forms.Label();
            this.status = new System.Windows.Forms.Label();
            this.buttons = new System.Windows.Forms.FlowLayoutPanel();
            this.closeBtn = new System.Windows.Forms.Button();
            this.saveBtn = new System.Windows.Forms.Button();
            this.intro = new System.Windows.Forms.Label();
            this.tips = new System.Windows.Forms.ToolTip(this.components);
            this.body.SuspendLayout();
            this.heapRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.heapMB)).BeginInit();
            this.playlistRow.SuspendLayout();
            this.moviesRow.SuspendLayout();
            this.movieButtons.SuspendLayout();
            this.buttons.SuspendLayout();
            this.SuspendLayout();
            //
            // body
            //
            this.body.AutoScroll = true;
            this.body.ColumnCount = 1;
            this.body.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.body.Controls.Add(this.textureHeading, 0, 0);
            this.body.Controls.Add(this.heapRow, 0, 1);
            this.body.Controls.Add(this.heapNote, 0, 2);
            this.body.Controls.Add(this.collideHeading, 0, 3);
            this.body.Controls.Add(this.npcCollide, 0, 4);
            this.body.Controls.Add(this.playerCollide, 0, 5);
            this.body.Controls.Add(this.collideNote, 0, 6);
            this.body.Controls.Add(this.loadingHeading, 0, 7);
            this.body.Controls.Add(this.playlistRow, 0, 8);
            this.body.Controls.Add(this.moviesOn, 0, 9);
            this.body.Controls.Add(this.moviesRow, 0, 10);
            this.body.Controls.Add(this.moviesToEnd, 0, 11);
            this.body.Controls.Add(this.playlistNote, 0, 12);
            this.body.Controls.Add(this.animHeading, 0, 13);
            this.body.Controls.Add(this.animOn, 0, 14);
            this.body.Controls.Add(this.capHeading, 0, 15);
            this.body.Controls.Add(this.capNote, 0, 16);
            this.body.Dock = System.Windows.Forms.DockStyle.Fill;
            this.body.Location = new System.Drawing.Point(0, 52);
            this.body.Name = "body";
            this.body.Padding = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.body.RowCount = 17;
            this.body.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.body.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.body.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.body.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.body.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.body.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.body.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.body.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.body.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.body.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.body.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.body.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.body.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.body.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.body.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.body.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.body.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.body.Size = new System.Drawing.Size(680, 574);
            this.body.TabIndex = 0;
            //
            // textureHeading
            //
            this.textureHeading.AutoSize = true;
            this.textureHeading.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.textureHeading.Location = new System.Drawing.Point(11, 0);
            this.textureHeading.Name = "textureHeading";
            this.textureHeading.Padding = new System.Windows.Forms.Padding(0, 10, 0, 2);
            this.textureHeading.Size = new System.Drawing.Size(101, 27);
            this.textureHeading.TabIndex = 0;
            this.textureHeading.Text = "Texture memory";
            //
            // heapRow
            //
            this.heapRow.AutoSize = true;
            this.heapRow.Controls.Add(this.heapOn);
            this.heapRow.Controls.Add(this.heapMB);
            this.heapRow.Controls.Add(this.heapUnit);
            this.heapRow.Location = new System.Drawing.Point(8, 27);
            this.heapRow.Margin = new System.Windows.Forms.Padding(0);
            this.heapRow.Name = "heapRow";
            this.heapRow.Size = new System.Drawing.Size(390, 26);
            this.heapRow.TabIndex = 1;
            this.heapRow.WrapContents = false;
            //
            // heapOn
            //
            this.heapOn.AutoSize = true;
            this.heapOn.Location = new System.Drawing.Point(3, 3);
            this.heapOn.Name = "heapOn";
            this.heapOn.Size = new System.Drawing.Size(279, 17);
            this.heapOn.TabIndex = 0;
            this.heapOn.Text = "Texture memory: the streaming texture heap is at least";
            this.tips.SetToolTip(this.heapOn, "The game reserves its streaming texture heap once, as it starts. Levels whose textures don't fit draw blocky 128 px copies; this raises the heap for this level's launches only, leaving every other level at the player's own value.");
            this.heapOn.UseVisualStyleBackColor = true;
            this.heapOn.CheckedChanged += new System.EventHandler(this.Setting_Changed);
            //
            // heapMB
            //
            this.heapMB.Increment = new decimal(new int[] {
            64,
            0,
            0,
            0});
            this.heapMB.Location = new System.Drawing.Point(288, 3);
            this.heapMB.Name = "heapMB";
            this.heapMB.Size = new System.Drawing.Size(70, 20);
            this.heapMB.TabIndex = 1;
            //
            // heapUnit
            //
            this.heapUnit.AutoSize = true;
            this.heapUnit.Location = new System.Drawing.Point(364, 0);
            this.heapUnit.Name = "heapUnit";
            this.heapUnit.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.heapUnit.Size = new System.Drawing.Size(23, 17);
            this.heapUnit.TabIndex = 2;
            this.heapUnit.Text = "MB";
            //
            // heapNote
            //
            this.heapNote.AutoSize = true;
            this.heapNote.Location = new System.Drawing.Point(11, 53);
            this.heapNote.MaximumSize = new System.Drawing.Size(620, 0);
            this.heapNote.Name = "heapNote";
            this.heapNote.Size = new System.Drawing.Size(0, 13);
            this.heapNote.TabIndex = 2;
            //
            // collideHeading
            //
            this.collideHeading.AutoSize = true;
            this.collideHeading.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.collideHeading.Location = new System.Drawing.Point(11, 66);
            this.collideHeading.Name = "collideHeading";
            this.collideHeading.Padding = new System.Windows.Forms.Padding(0, 10, 0, 2);
            this.collideHeading.Size = new System.Drawing.Size(113, 27);
            this.collideHeading.TabIndex = 3;
            this.collideHeading.Text = "Character collisions";
            //
            // npcCollide
            //
            this.npcCollide.AutoSize = true;
            this.npcCollide.Location = new System.Drawing.Point(11, 96);
            this.npcCollide.Name = "npcCollide";
            this.npcCollide.Size = new System.Drawing.Size(162, 17);
            this.npcCollide.TabIndex = 4;
            this.npcCollide.Text = "NPCs collide with each other";
            this.npcCollide.UseVisualStyleBackColor = true;
            this.npcCollide.CheckedChanged += new System.EventHandler(this.Setting_Changed);
            //
            // playerCollide
            //
            this.playerCollide.AutoSize = true;
            this.playerCollide.Location = new System.Drawing.Point(11, 119);
            this.playerCollide.Name = "playerCollide";
            this.playerCollide.Size = new System.Drawing.Size(166, 17);
            this.playerCollide.TabIndex = 5;
            this.playerCollide.Text = "The player collides with NPCs";
            this.playerCollide.UseVisualStyleBackColor = true;
            this.playerCollide.CheckedChanged += new System.EventHandler(this.Setting_Changed);
            //
            // collideNote
            //
            this.collideNote.AutoSize = true;
            this.collideNote.Location = new System.Drawing.Point(11, 139);
            this.collideNote.MaximumSize = new System.Drawing.Size(620, 0);
            this.collideNote.Name = "collideNote";
            this.collideNote.Size = new System.Drawing.Size(617, 26);
            this.collideNote.TabIndex = 6;
            this.collideNote.Text = "Off, characters walk through each other rather than blocking (crowded maps, many aliens). Shots, melee, the flamethrower, grabs and the alien's attacks still hit: they don't use the bodies that collide. Triggers, doors and the world are unchanged.";
            //
            // loadingHeading
            //
            this.loadingHeading.AutoSize = true;
            this.loadingHeading.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.loadingHeading.Location = new System.Drawing.Point(11, 165);
            this.loadingHeading.Name = "loadingHeading";
            this.loadingHeading.Padding = new System.Windows.Forms.Padding(0, 10, 0, 2);
            this.loadingHeading.Size = new System.Drawing.Size(90, 27);
            this.loadingHeading.TabIndex = 7;
            this.loadingHeading.Text = "Loading screen";
            //
            // playlistRow
            //
            this.playlistRow.AutoSize = true;
            this.playlistRow.Controls.Add(this.playlistOn);
            this.playlistRow.Controls.Add(this.playlist);
            this.playlistRow.Location = new System.Drawing.Point(8, 192);
            this.playlistRow.Margin = new System.Windows.Forms.Padding(0);
            this.playlistRow.Name = "playlistRow";
            this.playlistRow.Size = new System.Drawing.Size(448, 27);
            this.playlistRow.TabIndex = 8;
            this.playlistRow.WrapContents = false;
            //
            // playlistOn
            //
            this.playlistOn.AutoSize = true;
            this.playlistOn.Location = new System.Drawing.Point(3, 3);
            this.playlistOn.Name = "playlistOn";
            this.playlistOn.Size = new System.Drawing.Size(176, 17);
            this.playlistOn.TabIndex = 0;
            this.playlistOn.Text = "Loading screen: play the playlist";
            this.tips.SetToolTip(this.playlistOn, "A movie or image playlist from DATA\\GBL_ITEM.BML (Configuration Editors > Core Game > Loadscreen Movies edits them). It plays as the level loads from a launch, from the main menu's level select and from a checkpoint in another level.");
            this.playlistOn.UseVisualStyleBackColor = true;
            this.playlistOn.CheckedChanged += new System.EventHandler(this.playlistOn_CheckedChanged);
            //
            // playlist
            //
            this.playlist.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.playlist.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.playlist.Location = new System.Drawing.Point(185, 3);
            this.playlist.Name = "playlist";
            this.playlist.Size = new System.Drawing.Size(260, 21);
            this.playlist.TabIndex = 1;
            this.playlist.TextChanged += new System.EventHandler(this.Setting_Changed);
            //
            // moviesOn
            //
            this.moviesOn.AutoSize = true;
            this.moviesOn.Location = new System.Drawing.Point(11, 222);
            this.moviesOn.Name = "moviesOn";
            this.moviesOn.Size = new System.Drawing.Size(233, 17);
            this.moviesOn.TabIndex = 9;
            this.moviesOn.Text = "Loading screen: play the level's own movies";
            this.tips.SetToolTip(this.moviesOn, "Movies the level carries itself: .usm files (the CRI video the game plays) kept in DATA\\ENV\\<level>\\MOVIES, so a mod made from the level carries them. A launch into the level copies them into the game's movies and plays them as it loads; they come out again once the game closes.");
            this.moviesOn.UseVisualStyleBackColor = true;
            this.moviesOn.CheckedChanged += new System.EventHandler(this.moviesOn_CheckedChanged);
            //
            // moviesRow
            //
            this.moviesRow.AutoSize = true;
            this.moviesRow.Controls.Add(this.movies);
            this.moviesRow.Controls.Add(this.movieButtons);
            this.moviesRow.Location = new System.Drawing.Point(8, 242);
            this.moviesRow.Margin = new System.Windows.Forms.Padding(0);
            this.moviesRow.Name = "moviesRow";
            this.moviesRow.Padding = new System.Windows.Forms.Padding(18, 0, 0, 0);
            this.moviesRow.Size = new System.Drawing.Size(370, 70);
            this.moviesRow.TabIndex = 10;
            this.moviesRow.WrapContents = false;
            //
            // movies
            //
            this.movies.IntegralHeight = false;
            this.movies.Location = new System.Drawing.Point(21, 3);
            this.movies.Name = "movies";
            this.movies.Size = new System.Drawing.Size(260, 64);
            this.movies.TabIndex = 0;
            //
            // movieButtons
            //
            this.movieButtons.AutoSize = true;
            this.movieButtons.Controls.Add(this.addMovieBtn);
            this.movieButtons.Controls.Add(this.removeMovieBtn);
            this.movieButtons.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.movieButtons.Location = new System.Drawing.Point(284, 0);
            this.movieButtons.Margin = new System.Windows.Forms.Padding(0);
            this.movieButtons.Name = "movieButtons";
            this.movieButtons.Size = new System.Drawing.Size(86, 60);
            this.movieButtons.TabIndex = 1;
            //
            // addMovieBtn
            //
            this.addMovieBtn.Location = new System.Drawing.Point(3, 3);
            this.addMovieBtn.Name = "addMovieBtn";
            this.addMovieBtn.Size = new System.Drawing.Size(80, 24);
            this.addMovieBtn.TabIndex = 0;
            this.addMovieBtn.Text = "Add";
            this.tips.SetToolTip(this.addMovieBtn, "Copy a .usm movie into the level's MOVIES folder and add it to the list.");
            this.addMovieBtn.UseVisualStyleBackColor = true;
            this.addMovieBtn.Click += new System.EventHandler(this.addMovieBtn_Click);
            //
            // removeMovieBtn
            //
            this.removeMovieBtn.Location = new System.Drawing.Point(3, 33);
            this.removeMovieBtn.Name = "removeMovieBtn";
            this.removeMovieBtn.Size = new System.Drawing.Size(80, 24);
            this.removeMovieBtn.TabIndex = 1;
            this.removeMovieBtn.Text = "Remove";
            this.tips.SetToolTip(this.removeMovieBtn, "Take the selected movie off the list (its file stays in the level's MOVIES folder).");
            this.removeMovieBtn.UseVisualStyleBackColor = true;
            this.removeMovieBtn.Click += new System.EventHandler(this.removeMovieBtn_Click);
            //
            // moviesToEnd
            //
            this.moviesToEnd.AutoSize = true;
            this.moviesToEnd.Location = new System.Drawing.Point(29, 315);
            this.moviesToEnd.Margin = new System.Windows.Forms.Padding(21, 3, 3, 3);
            this.moviesToEnd.Name = "moviesToEnd";
            this.moviesToEnd.Size = new System.Drawing.Size(442, 17);
            this.moviesToEnd.TabIndex = 11;
            this.moviesToEnd.Text = "Play them to the end: the level waits for them (once it has loaded, holding a button skips)";
            this.tips.SetToolTip(this.moviesToEnd, "For an intro: the launch changes the game's loading screen to wait for the last movie (for that launch only). Without it the movies loop until the level has loaded, as the game's own loading footage does.");
            this.moviesToEnd.UseVisualStyleBackColor = true;
            this.moviesToEnd.CheckedChanged += new System.EventHandler(this.Setting_Changed);
            //
            // playlistNote
            //
            this.playlistNote.AutoSize = true;
            this.playlistNote.Location = new System.Drawing.Point(11, 335);
            this.playlistNote.MaximumSize = new System.Drawing.Size(620, 0);
            this.playlistNote.Name = "playlistNote";
            this.playlistNote.Size = new System.Drawing.Size(0, 13);
            this.playlistNote.TabIndex = 12;
            //
            // animHeading
            //
            this.animHeading.AutoSize = true;
            this.animHeading.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.animHeading.Location = new System.Drawing.Point(11, 348);
            this.animHeading.Name = "animHeading";
            this.animHeading.Padding = new System.Windows.Forms.Padding(0, 10, 0, 2);
            this.animHeading.Size = new System.Drawing.Size(114, 27);
            this.animHeading.TabIndex = 13;
            this.animHeading.Text = "Animation memory";
            //
            // animOn
            //
            this.animOn.AutoSize = true;
            this.animOn.Location = new System.Drawing.Point(11, 378);
            this.animOn.Name = "animOn";
            this.animOn.Size = new System.Drawing.Size(290, 17);
            this.animOn.TabIndex = 14;
            this.animOn.Text = "Animation memory: double the game's animation memory";
            this.tips.SetToolTip(this.animOn, "Launch Game's Allow larger animation memory, for this level's launches: a character spawns only once its animation set's resident clips fit in the game's animation memory, so a level with custom animation sets or new animation contexts may need it. Every other level keeps the player's own choice.");
            this.animOn.UseVisualStyleBackColor = true;
            this.animOn.CheckedChanged += new System.EventHandler(this.Setting_Changed);
            //
            // capHeading
            //
            this.capHeading.AutoSize = true;
            this.capHeading.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.capHeading.Location = new System.Drawing.Point(11, 398);
            this.capHeading.Name = "capHeading";
            this.capHeading.Padding = new System.Windows.Forms.Padding(0, 10, 0, 2);
            this.capHeading.Size = new System.Drawing.Size(110, 27);
            this.capHeading.TabIndex = 15;
            this.capHeading.Text = "Characters at once";
            //
            // capNote
            //
            this.capNote.AutoSize = true;
            this.capNote.Location = new System.Drawing.Point(11, 425);
            this.capNote.MaximumSize = new System.Drawing.Size(620, 0);
            this.capNote.Name = "capNote";
            this.capNote.Size = new System.Drawing.Size(0, 13);
            this.capNote.TabIndex = 16;
            //
            // status
            //
            this.status.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.status.Location = new System.Drawing.Point(0, 626);
            this.status.Name = "status";
            this.status.Padding = new System.Windows.Forms.Padding(8, 2, 8, 0);
            this.status.Size = new System.Drawing.Size(680, 36);
            this.status.TabIndex = 1;
            //
            // buttons
            //
            this.buttons.Controls.Add(this.closeBtn);
            this.buttons.Controls.Add(this.saveBtn);
            this.buttons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.buttons.Location = new System.Drawing.Point(0, 662);
            this.buttons.Name = "buttons";
            this.buttons.Padding = new System.Windows.Forms.Padding(4);
            this.buttons.Size = new System.Drawing.Size(680, 38);
            this.buttons.TabIndex = 2;
            //
            // closeBtn
            //
            this.closeBtn.Location = new System.Drawing.Point(579, 7);
            this.closeBtn.Name = "closeBtn";
            this.closeBtn.Size = new System.Drawing.Size(90, 26);
            this.closeBtn.TabIndex = 0;
            this.closeBtn.Text = "Close";
            this.closeBtn.UseVisualStyleBackColor = true;
            this.closeBtn.Click += new System.EventHandler(this.closeBtn_Click);
            //
            // saveBtn
            //
            this.saveBtn.Location = new System.Drawing.Point(483, 7);
            this.saveBtn.Name = "saveBtn";
            this.saveBtn.Size = new System.Drawing.Size(90, 26);
            this.saveBtn.TabIndex = 1;
            this.saveBtn.Text = "Save";
            this.saveBtn.UseVisualStyleBackColor = true;
            this.saveBtn.Click += new System.EventHandler(this.saveBtn_Click);
            //
            // intro
            //
            this.intro.Dock = System.Windows.Forms.DockStyle.Top;
            this.intro.Location = new System.Drawing.Point(0, 0);
            this.intro.Name = "intro";
            this.intro.Padding = new System.Windows.Forms.Padding(8, 8, 8, 0);
            this.intro.Size = new System.Drawing.Size(680, 52);
            this.intro.TabIndex = 3;
            this.intro.Text = "What this level needs from the game while it is played. A launch into it from OpenCAGE (Launch Game, Launch Mod) sets the game up so, and the game's own come back once the game closes: every other level, and the game started any other way, run as they ship.";
            //
            // tips
            //
            this.tips.AutoPopDelay = 20000;
            //
            // LevelLaunchSettingsEditor
            //
            this.AcceptButton = this.saveBtn;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.closeBtn;
            this.ClientSize = new System.Drawing.Size(680, 700);
            this.Controls.Add(this.body);
            this.Controls.Add(this.status);
            this.Controls.Add(this.buttons);
            this.Controls.Add(this.intro);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(560, 420);
            this.Name = "LevelLaunchSettingsEditor";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Level Launch Settings";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.LevelLaunchSettingsEditor_FormClosed);
            this.Load += new System.EventHandler(this.LevelLaunchSettingsEditor_Load);
            this.body.ResumeLayout(false);
            this.body.PerformLayout();
            this.heapRow.ResumeLayout(false);
            this.heapRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.heapMB)).EndInit();
            this.playlistRow.ResumeLayout(false);
            this.playlistRow.PerformLayout();
            this.moviesRow.ResumeLayout(false);
            this.moviesRow.PerformLayout();
            this.movieButtons.ResumeLayout(false);
            this.buttons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel body;
        private System.Windows.Forms.Label textureHeading;
        private System.Windows.Forms.FlowLayoutPanel heapRow;
        private System.Windows.Forms.CheckBox heapOn;
        private System.Windows.Forms.NumericUpDown heapMB;
        private System.Windows.Forms.Label heapUnit;
        private System.Windows.Forms.Label heapNote;
        private System.Windows.Forms.Label collideHeading;
        private System.Windows.Forms.CheckBox npcCollide;
        private System.Windows.Forms.CheckBox playerCollide;
        private System.Windows.Forms.Label collideNote;
        private System.Windows.Forms.Label loadingHeading;
        private System.Windows.Forms.FlowLayoutPanel playlistRow;
        private System.Windows.Forms.CheckBox playlistOn;
        private System.Windows.Forms.ComboBox playlist;
        private System.Windows.Forms.CheckBox moviesOn;
        private System.Windows.Forms.FlowLayoutPanel moviesRow;
        private System.Windows.Forms.ListBox movies;
        private System.Windows.Forms.FlowLayoutPanel movieButtons;
        private System.Windows.Forms.Button addMovieBtn;
        private System.Windows.Forms.Button removeMovieBtn;
        private System.Windows.Forms.CheckBox moviesToEnd;
        private System.Windows.Forms.Label playlistNote;
        private System.Windows.Forms.Label animHeading;
        private System.Windows.Forms.CheckBox animOn;
        private System.Windows.Forms.Label capHeading;
        private System.Windows.Forms.Label capNote;
        private System.Windows.Forms.Label status;
        private System.Windows.Forms.FlowLayoutPanel buttons;
        private System.Windows.Forms.Button closeBtn;
        private System.Windows.Forms.Button saveBtn;
        private System.Windows.Forms.Label intro;
        private System.Windows.Forms.ToolTip tips;
    }
}

namespace Alien_Isolation_Mod_Tools
{
    partial class LoadMovieEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LoadMovieEditor));
            this.label78 = new System.Windows.Forms.Label();
            this.btnSave = new System.Windows.Forms.Button();
            this.moviePlaylists = new System.Windows.Forms.ComboBox();
            this.btnSelectClass = new System.Windows.Forms.Button();
            this.label21 = new System.Windows.Forms.Label();
            this.terminate_on_load_completed = new System.Windows.Forms.ComboBox();
            this.label8 = new System.Windows.Forms.Label();
            this.allow_player_to_skip = new System.Windows.Forms.ComboBox();
            this.label9 = new System.Windows.Forms.Label();
            this.loop_playlist = new System.Windows.Forms.ComboBox();
            this.label10 = new System.Windows.Forms.Label();
            this.shuffle_playlist = new System.Windows.Forms.ComboBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.dev_comments = new System.Windows.Forms.TextBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.movieList = new System.Windows.Forms.ListBox();
            this.addMovie = new System.Windows.Forms.Button();
            this.removeMovie = new System.Windows.Forms.Button();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.groupBox1.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // label78
            // 
            this.label78.AutoSize = true;
            this.label78.Font = new System.Drawing.Font("Microsoft Sans Serif", 17F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label78.Location = new System.Drawing.Point(37, 9);
            this.label78.Name = "label78";
            this.label78.Size = new System.Drawing.Size(392, 29);
            this.label78.TabIndex = 327;
            this.label78.Text = "Alien: Isolation Movie Playlist Editor";
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(302, 437);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(150, 35);
            this.btnSave.TabIndex = 326;
            this.btnSave.Text = "Save";
            this.toolTip1.SetToolTip(this.btnSave, "Save playlist settings.");
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // moviePlaylists
            // 
            this.moviePlaylists.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.moviePlaylists.Enabled = false;
            this.moviePlaylists.FormattingEnabled = true;
            this.moviePlaylists.Items.AddRange(new object[] {
            "DEFAULTS",
            "THE_PLAYER",
            "ALIEN",
            "ANDROID",
            "CIVILIAN",
            "SECURITY_GUARD",
            "CUTSCENE",
            "FACEHUGGER",
            "SPACESUIT_NPC",
            "RIOT_GUARD",
            "ANDROID_HEAVY",
            "MELEE_HUMAN",
            "INNOCENT",
            "CUTSCENE_ANDROID"});
            this.moviePlaylists.Location = new System.Drawing.Point(12, 45);
            this.moviePlaylists.Name = "moviePlaylists";
            this.moviePlaylists.Size = new System.Drawing.Size(327, 21);
            this.moviePlaylists.TabIndex = 325;
            this.toolTip1.SetToolTip(this.moviePlaylists, "All movie playlists.");
            // 
            // btnSelectClass
            // 
            this.btnSelectClass.Enabled = false;
            this.btnSelectClass.Location = new System.Drawing.Point(345, 44);
            this.btnSelectClass.Name = "btnSelectClass";
            this.btnSelectClass.Size = new System.Drawing.Size(106, 23);
            this.btnSelectClass.TabIndex = 324;
            this.btnSelectClass.Text = "Load Playlist";
            this.toolTip1.SetToolTip(this.btnSelectClass, "Load selected playlist.");
            this.btnSelectClass.UseVisualStyleBackColor = true;
            this.btnSelectClass.Click += new System.EventHandler(this.btnSelectClass_Click);
            // 
            // label21
            // 
            this.label21.AutoSize = true;
            this.label21.Location = new System.Drawing.Point(17, 27);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(182, 13);
            this.label21.TabIndex = 353;
            this.label21.Text = "End Playlist When Loading Finished?";
            // 
            // terminate_on_load_completed
            // 
            this.terminate_on_load_completed.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.terminate_on_load_completed.Enabled = false;
            this.terminate_on_load_completed.FormattingEnabled = true;
            this.terminate_on_load_completed.Items.AddRange(new object[] {
            "true",
            "false"});
            this.terminate_on_load_completed.Location = new System.Drawing.Point(20, 43);
            this.terminate_on_load_completed.Name = "terminate_on_load_completed";
            this.terminate_on_load_completed.Size = new System.Drawing.Size(187, 21);
            this.terminate_on_load_completed.TabIndex = 354;
            this.toolTip1.SetToolTip(this.terminate_on_load_completed, "Should the playlist end when the level has finished loading in the background?");
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(17, 67);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(110, 13);
            this.label8.TabIndex = 355;
            this.label8.Text = "Allow Player To Skip?";
            // 
            // allow_player_to_skip
            // 
            this.allow_player_to_skip.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.allow_player_to_skip.Enabled = false;
            this.allow_player_to_skip.FormattingEnabled = true;
            this.allow_player_to_skip.Items.AddRange(new object[] {
            "true",
            "false"});
            this.allow_player_to_skip.Location = new System.Drawing.Point(20, 83);
            this.allow_player_to_skip.Name = "allow_player_to_skip";
            this.allow_player_to_skip.Size = new System.Drawing.Size(187, 21);
            this.allow_player_to_skip.TabIndex = 356;
            this.toolTip1.SetToolTip(this.allow_player_to_skip, "Should the player be able to skip this movie playlist?");
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(231, 67);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(72, 13);
            this.label9.TabIndex = 359;
            this.label9.Text = "Loop Playlist?";
            // 
            // loop_playlist
            // 
            this.loop_playlist.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.loop_playlist.Enabled = false;
            this.loop_playlist.FormattingEnabled = true;
            this.loop_playlist.Items.AddRange(new object[] {
            "true",
            "false"});
            this.loop_playlist.Location = new System.Drawing.Point(234, 83);
            this.loop_playlist.Name = "loop_playlist";
            this.loop_playlist.Size = new System.Drawing.Size(187, 21);
            this.loop_playlist.TabIndex = 360;
            this.toolTip1.SetToolTip(this.loop_playlist, "Should this playlist loop to play movies multiple times?");
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(231, 27);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(81, 13);
            this.label10.TabIndex = 357;
            this.label10.Text = "Shuffle Playlist?";
            // 
            // shuffle_playlist
            // 
            this.shuffle_playlist.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.shuffle_playlist.Enabled = false;
            this.shuffle_playlist.FormattingEnabled = true;
            this.shuffle_playlist.Items.AddRange(new object[] {
            "true",
            "false"});
            this.shuffle_playlist.Location = new System.Drawing.Point(234, 43);
            this.shuffle_playlist.Name = "shuffle_playlist";
            this.shuffle_playlist.Size = new System.Drawing.Size(187, 21);
            this.shuffle_playlist.TabIndex = 358;
            this.toolTip1.SetToolTip(this.shuffle_playlist, "Should this playlist shuffle in order of movies?");
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label21);
            this.groupBox1.Controls.Add(this.label9);
            this.groupBox1.Controls.Add(this.terminate_on_load_completed);
            this.groupBox1.Controls.Add(this.loop_playlist);
            this.groupBox1.Controls.Add(this.allow_player_to_skip);
            this.groupBox1.Controls.Add(this.label10);
            this.groupBox1.Controls.Add(this.label8);
            this.groupBox1.Controls.Add(this.shuffle_playlist);
            this.groupBox1.Location = new System.Drawing.Point(12, 73);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(439, 120);
            this.groupBox1.TabIndex = 330;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Playlist Settings";
            // 
            // dev_comments
            // 
            this.dev_comments.Location = new System.Drawing.Point(13, 437);
            this.dev_comments.Multiline = true;
            this.dev_comments.Name = "dev_comments";
            this.dev_comments.ReadOnly = true;
            this.dev_comments.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.dev_comments.Size = new System.Drawing.Size(283, 35);
            this.dev_comments.TabIndex = 347;
            this.toolTip1.SetToolTip(this.dev_comments, "Comments about this playlist from the developers.");
            // 
            // movieList
            // 
            this.movieList.Enabled = false;
            this.movieList.FormattingEnabled = true;
            this.movieList.Location = new System.Drawing.Point(6, 19);
            this.movieList.Name = "movieList";
            this.movieList.Size = new System.Drawing.Size(427, 173);
            this.movieList.TabIndex = 348;
            this.toolTip1.SetToolTip(this.movieList, "All movies in playlist.");
            // 
            // addMovie
            // 
            this.addMovie.Enabled = false;
            this.addMovie.Location = new System.Drawing.Point(177, 198);
            this.addMovie.Name = "addMovie";
            this.addMovie.Size = new System.Drawing.Size(256, 28);
            this.addMovie.TabIndex = 347;
            this.addMovie.Text = "Add New Movie";
            this.toolTip1.SetToolTip(this.addMovie, "Add a new movie to the playlist (opens a file input window).");
            this.addMovie.UseVisualStyleBackColor = true;
            this.addMovie.Click += new System.EventHandler(this.button1_Click);
            // 
            // removeMovie
            // 
            this.removeMovie.Enabled = false;
            this.removeMovie.Location = new System.Drawing.Point(6, 198);
            this.removeMovie.Name = "removeMovie";
            this.removeMovie.Size = new System.Drawing.Size(165, 28);
            this.removeMovie.TabIndex = 349;
            this.removeMovie.Text = "Remove Selected Movie";
            this.toolTip1.SetToolTip(this.removeMovie, "Remove the selected movie from the loaded playlist.");
            this.removeMovie.UseVisualStyleBackColor = true;
            this.removeMovie.Click += new System.EventHandler(this.removeMovie_Click);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.movieList);
            this.groupBox3.Controls.Add(this.removeMovie);
            this.groupBox3.Controls.Add(this.addMovie);
            this.groupBox3.Location = new System.Drawing.Point(12, 199);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(439, 232);
            this.groupBox3.TabIndex = 347;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Movies In Playlist";
            // 
            // LoadMovieEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(462, 479);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.dev_comments);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.label78);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.moviePlaylists);
            this.Controls.Add(this.btnSelectClass);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "LoadMovieEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Alien: Isolation Movie Playlist Editor";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label label78;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.ComboBox moviePlaylists;
        private System.Windows.Forms.Button btnSelectClass;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.ComboBox terminate_on_load_completed;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.ComboBox allow_player_to_skip;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.ComboBox loop_playlist;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.ComboBox shuffle_playlist;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox dev_comments;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.ListBox movieList;
        private System.Windows.Forms.Button addMovie;
        private System.Windows.Forms.Button removeMovie;
        private System.Windows.Forms.GroupBox groupBox3;
    }
}
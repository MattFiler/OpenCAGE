namespace CommandsEditor.ConfigEditors
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LoadMovieEditor));
            this.funcHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.inheritHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.helpBtn = new System.Windows.Forms.Button();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.movieList = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.moveMovieDown = new System.Windows.Forms.Button();
            this.moveMovieUp = new System.Windows.Forms.Button();
            this.removeMovie = new System.Windows.Forms.Button();
            this.addMovie = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.endWhenLoaded = new System.Windows.Forms.CheckBox();
            this.loop = new System.Windows.Forms.CheckBox();
            this.allowSkip = new System.Windows.Forms.CheckBox();
            this.shuffle = new System.Windows.Forms.CheckBox();
            this.btnSave = new System.Windows.Forms.Button();
            this.moviePlaylists = new System.Windows.Forms.ComboBox();
            this.groupBox3.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // funcHeader
            // 
            this.funcHeader.Text = "Entity";
            this.funcHeader.Width = 595;
            // 
            // inheritHeader
            // 
            this.inheritHeader.Text = "Delay";
            this.inheritHeader.Width = 48;
            // 
            // helpBtn
            // 
            this.helpBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.helpBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.helpBtn.Image = ((System.Drawing.Image)(resources.GetObject("helpBtn.Image")));
            this.helpBtn.Location = new System.Drawing.Point(440, 0);
            this.helpBtn.Name = "helpBtn";
            this.helpBtn.Size = new System.Drawing.Size(20, 20);
            this.helpBtn.TabIndex = 372;
            this.helpBtn.UseVisualStyleBackColor = true;
            this.helpBtn.Click += new System.EventHandler(this.helpBtn_Click);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.movieList);
            this.groupBox3.Controls.Add(this.moveMovieDown);
            this.groupBox3.Controls.Add(this.moveMovieUp);
            this.groupBox3.Controls.Add(this.removeMovie);
            this.groupBox3.Controls.Add(this.addMovie);
            this.groupBox3.Location = new System.Drawing.Point(12, 113);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(439, 232);
            this.groupBox3.TabIndex = 347;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Movies In Playlist";
            // 
            // movieList
            // 
            this.movieList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1});
            this.movieList.FullRowSelect = true;
            this.movieList.HideSelection = false;
            this.movieList.Location = new System.Drawing.Point(6, 19);
            this.movieList.MultiSelect = false;
            this.movieList.Name = "movieList";
            this.movieList.Size = new System.Drawing.Size(427, 173);
            this.movieList.TabIndex = 348;
            this.movieList.UseCompatibleStateImageBehavior = false;
            this.movieList.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "Movie";
            this.columnHeader1.Width = 366;
            // 
            // moveMovieDown
            // 
            this.moveMovieDown.Location = new System.Drawing.Point(353, 197);
            this.moveMovieDown.Name = "moveMovieDown";
            this.moveMovieDown.Size = new System.Drawing.Size(80, 28);
            this.moveMovieDown.TabIndex = 351;
            this.moveMovieDown.Text = "Move Down";
            this.moveMovieDown.UseVisualStyleBackColor = true;
            this.moveMovieDown.Click += new System.EventHandler(this.moveMovieDown_Click);
            // 
            // moveMovieUp
            // 
            this.moveMovieUp.Location = new System.Drawing.Point(267, 197);
            this.moveMovieUp.Name = "moveMovieUp";
            this.moveMovieUp.Size = new System.Drawing.Size(80, 28);
            this.moveMovieUp.TabIndex = 350;
            this.moveMovieUp.Text = "Move Up";
            this.moveMovieUp.UseVisualStyleBackColor = true;
            this.moveMovieUp.Click += new System.EventHandler(this.moveMovieUp_Click);
            // 
            // removeMovie
            // 
            this.removeMovie.Location = new System.Drawing.Point(181, 197);
            this.removeMovie.Name = "removeMovie";
            this.removeMovie.Size = new System.Drawing.Size(80, 28);
            this.removeMovie.TabIndex = 349;
            this.removeMovie.Text = "Remove";
            this.removeMovie.UseVisualStyleBackColor = true;
            this.removeMovie.Click += new System.EventHandler(this.removeMovie_Click);
            // 
            // addMovie
            // 
            this.addMovie.Location = new System.Drawing.Point(6, 197);
            this.addMovie.Name = "addMovie";
            this.addMovie.Size = new System.Drawing.Size(121, 28);
            this.addMovie.TabIndex = 347;
            this.addMovie.Text = "Add New";
            this.addMovie.UseVisualStyleBackColor = true;
            this.addMovie.Click += new System.EventHandler(this.addMovie_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.endWhenLoaded);
            this.groupBox1.Controls.Add(this.loop);
            this.groupBox1.Controls.Add(this.allowSkip);
            this.groupBox1.Controls.Add(this.shuffle);
            this.groupBox1.Location = new System.Drawing.Point(12, 39);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(439, 68);
            this.groupBox1.TabIndex = 330;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Playlist Settings";
            // 
            // endWhenLoaded
            // 
            this.endWhenLoaded.AutoSize = true;
            this.endWhenLoaded.Location = new System.Drawing.Point(95, 20);
            this.endWhenLoaded.Name = "endWhenLoaded";
            this.endWhenLoaded.Size = new System.Drawing.Size(195, 17);
            this.endWhenLoaded.TabIndex = 348;
            this.endWhenLoaded.Text = "End Playlist When Loading Finished";
            this.endWhenLoaded.UseVisualStyleBackColor = true;
            // 
            // loop
            // 
            this.loop.AutoSize = true;
            this.loop.Location = new System.Drawing.Point(95, 43);
            this.loop.Name = "loop";
            this.loop.Size = new System.Drawing.Size(50, 17);
            this.loop.TabIndex = 350;
            this.loop.Text = "Loop";
            this.loop.UseVisualStyleBackColor = true;
            // 
            // allowSkip
            // 
            this.allowSkip.AutoSize = true;
            this.allowSkip.Location = new System.Drawing.Point(10, 20);
            this.allowSkip.Name = "allowSkip";
            this.allowSkip.Size = new System.Drawing.Size(75, 17);
            this.allowSkip.TabIndex = 351;
            this.allowSkip.Text = "Allow Skip";
            this.allowSkip.UseVisualStyleBackColor = true;
            // 
            // shuffle
            // 
            this.shuffle.AutoSize = true;
            this.shuffle.Location = new System.Drawing.Point(10, 43);
            this.shuffle.Name = "shuffle";
            this.shuffle.Size = new System.Drawing.Size(59, 17);
            this.shuffle.TabIndex = 349;
            this.shuffle.Text = "Shuffle";
            this.shuffle.UseVisualStyleBackColor = true;
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(315, 351);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(136, 33);
            this.btnSave.TabIndex = 326;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // moviePlaylists
            // 
            this.moviePlaylists.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.moviePlaylists.FormattingEnabled = true;
            this.moviePlaylists.Location = new System.Drawing.Point(12, 12);
            this.moviePlaylists.Name = "moviePlaylists";
            this.moviePlaylists.Size = new System.Drawing.Size(439, 21);
            this.moviePlaylists.TabIndex = 325;
            this.moviePlaylists.SelectedIndexChanged += new System.EventHandler(this.moviePlaylists_SelectedIndexChanged);
            // 
            // LoadMovieEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(460, 394);
            this.Controls.Add(this.helpBtn);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.moviePlaylists);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "LoadMovieEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Loadscreen Playlist Editor";
            this.groupBox3.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.ComboBox moviePlaylists;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button addMovie;
        private System.Windows.Forms.Button removeMovie;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Button moveMovieDown;
        private System.Windows.Forms.Button moveMovieUp;
        private System.Windows.Forms.ListView movieList;
        private System.Windows.Forms.ColumnHeader funcHeader;
        private System.Windows.Forms.ColumnHeader inheritHeader;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.CheckBox endWhenLoaded;
        private System.Windows.Forms.CheckBox loop;
        private System.Windows.Forms.CheckBox allowSkip;
        private System.Windows.Forms.CheckBox shuffle;
        private System.Windows.Forms.Button helpBtn;
    }
}

using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// A level's launch settings (LevelLaunchSettings): its texture heap, whether characters collide with each other, and its loading
    /// playlist. Used from the next launch into the level; everything else keeps the game's own.
    /// </summary>
    public class LevelLaunchSettingsEditor : BaseWindow
    {
        private readonly string _level;

        private readonly Label intro = new Label { Dock = DockStyle.Top, Height = 52, Padding = new Padding(8, 8, 8, 0) };
        private readonly TableLayoutPanel body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(8, 0, 8, 0), AutoScroll = true };
        private readonly FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(4) };
        private readonly Button closeBtn = new Button { Text = "Close", Width = 90, Height = 26 };
        private readonly Button saveBtn = new Button { Text = "Save", Width = 90, Height = 26 };
        private readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(8, 2, 8, 0) };

        private readonly CheckBox heapOn = new CheckBox { AutoSize = true, Text = "Texture memory: the streaming texture heap is at least" };
        private readonly NumericUpDown heapMB = new NumericUpDown { Width = 70, Minimum = LevelLaunchSettings.MinHeapMB, Maximum = LevelLaunchSettings.MaxHeapMB, Increment = 64, Value = 512 };
        private readonly Label heapNote = new Label { AutoSize = true, MaximumSize = new Size(620, 0) };

        private readonly CheckBox npcCollide = new CheckBox { AutoSize = true, Text = "NPCs collide with each other" };
        private readonly CheckBox playerCollide = new CheckBox { AutoSize = true, Text = "The player collides with NPCs" };
        private readonly Label collideNote = new Label { AutoSize = true, MaximumSize = new Size(620, 0) };

        private readonly CheckBox playlistOn = new CheckBox { AutoSize = true, Text = "Loading screen: play the playlist" };
        private readonly ComboBox playlist = new ComboBox { Width = 260, DropDownStyle = ComboBoxStyle.DropDown, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
        private readonly Label playlistNote = new Label { AutoSize = true, MaximumSize = new Size(620, 0) };
        private readonly CheckBox moviesOn = new CheckBox { AutoSize = true, Text = "Loading screen: play the level's own movies" };
        private readonly ListBox movies = new ListBox { Width = 260, Height = 64, IntegralHeight = false };
        private readonly Button addMovieBtn = new Button { Text = "Add", Width = 80, Height = 24 };
        private readonly Button removeMovieBtn = new Button { Text = "Remove", Width = 80, Height = 24 };
        private readonly CheckBox moviesToEnd = new CheckBox { AutoSize = true, Text = "Play them to the end: the level waits for them (once it has loaded, holding a button skips)" };

        private readonly CheckBox animOn = new CheckBox { AutoSize = true, Text = "Animation memory: double the game's animation memory" };
        private readonly Label capNote = new Label { AutoSize = true, MaximumSize = new Size(620, 0) };
        private readonly ToolTip tips = new ToolTip() { AutoPopDelay = 20000 };
        private bool _filling;

        /// <summary>Show a level's launch settings (one window per level: an open one comes to the front).</summary>
        public static void ShowFor(IWin32Window owner, string level)
        {
            if (string.IsNullOrWhiteSpace(level))
            {
                MessageBox.Show("Open a level to see its launch settings.", "Level Launch Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            LevelLaunchSettingsEditor open = Application.OpenForms.OfType<LevelLaunchSettingsEditor>().FirstOrDefault(o => string.Equals(o._level, level.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));
            if (open != null)
            {
                open.Activate();
                return;
            }
            LevelLaunchSettingsEditor editor = new LevelLaunchSettingsEditor(level);
            if (owner is Form form && form.TopMost)
                editor.TopMost = true;
            editor.Show(owner);
        }

        public LevelLaunchSettingsEditor(string level)
        {
            _level = level.Replace('\\', '/');

            Text = "Level Launch Settings - " + _level;
            Icon = SharedFormIcon.Icon;
            ClientSize = new Size(680, 700);
            MinimumSize = new Size(560, 420);
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            MinimizeBox = false;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/level-launch-settings/");

            intro.Text = "What this level needs from the game while it is played. A launch into it from OpenCAGE (Launch Game, Launch Mod) sets the game up so, " +
                         "and the game's own come back once the game closes: every other level, and the game started any other way, run as they ship.";

            FlowLayoutPanel heapRow = Row(heapOn, heapMB, new Label { AutoSize = true, Text = "MB", Padding = new Padding(0, 4, 0, 0) });
            FlowLayoutPanel playlistRow = Row(playlistOn, playlist);
            FlowLayoutPanel movieButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0) };
            movieButtons.Controls.AddRange(new Control[] { addMovieBtn, removeMovieBtn });
            FlowLayoutPanel moviesRow = Row(movies, movieButtons);
            moviesRow.Padding = new Padding(18, 0, 0, 0);
            moviesToEnd.Margin = new Padding(21, 3, 3, 3);
            body.Controls.Add(Heading("Texture memory"));
            body.Controls.Add(heapRow);
            body.Controls.Add(heapNote);
            body.Controls.Add(Heading("Character collisions"));
            body.Controls.Add(npcCollide);
            body.Controls.Add(playerCollide);
            body.Controls.Add(collideNote);
            body.Controls.Add(Heading("Loading screen"));
            body.Controls.Add(playlistRow);
            body.Controls.Add(moviesOn);
            body.Controls.Add(moviesRow);
            body.Controls.Add(moviesToEnd);
            body.Controls.Add(playlistNote);
            body.Controls.Add(Heading("Animation memory"));
            body.Controls.Add(animOn);
            body.Controls.Add(Heading("Characters at once"));
            body.Controls.Add(capNote);

            collideNote.Text = "Off, characters walk through each other rather than blocking (crowded maps, many aliens). Shots, melee, the flamethrower, " +
                               "grabs and the alien's attacks still hit: they don't use the bodies that collide. Triggers, doors and the world are unchanged.";
            capNote.Text = "The game holds " + CharacterCapRule.Pool + " characters at once, the player and every corpse not yet despawned included. " +
                           "That can't be raised: the game sizes its character, AI and animation tables to it. OpenCAGE's launches stop the crash that spawning past it causes: " +
                           "a spawn with no free slot waits (despawning the corpse furthest out of view, as the game does), and characters spawning in the same moment that find none left are left out. " +
                           "Level Check warns when a level can reach it.";

            tips.SetToolTip(heapOn, "The game reserves its streaming texture heap once, as it starts. Levels whose textures don't fit draw blocky 128 px copies; " +
                                    "this raises the heap for this level's launches only, leaving every other level at the player's own value.");
            tips.SetToolTip(moviesOn, "Movies the level carries itself: .usm files (the CRI video the game plays) kept in DATA\\ENV\\<level>\\MOVIES, so a mod made from the level " +
                                      "carries them. A launch into the level copies them into the game's movies and plays them as it loads; they come out again once the game closes.");
            tips.SetToolTip(addMovieBtn, "Copy a .usm movie into the level's MOVIES folder and add it to the list.");
            tips.SetToolTip(removeMovieBtn, "Take the selected movie off the list (its file stays in the level's MOVIES folder).");
            tips.SetToolTip(moviesToEnd, "For an intro: the launch changes the game's loading screen to wait for the last movie (for that launch only). " +
                                         "Without it the movies loop until the level has loaded, as the game's own loading footage does.");
            addMovieBtn.Click += (s, e) => AddMovie();
            removeMovieBtn.Click += (s, e) =>
            {
                if (movies.SelectedIndex < 0) return;
                movies.Items.RemoveAt(movies.SelectedIndex);
                Changed();
            };
            //One or the other: a playlist of the game's, or the level's own movies
            playlistOn.CheckedChanged += (s, e) => { if (playlistOn.Checked) moviesOn.Checked = false; };
            moviesOn.CheckedChanged += (s, e) => { if (moviesOn.Checked) playlistOn.Checked = false; Changed(); };
            moviesToEnd.CheckedChanged += (s, e) => Changed();
            tips.SetToolTip(animOn, "Launch Game's Allow larger animation memory, for this level's launches: a character spawns only once its animation set's resident clips fit in the game's animation memory, " +
                                    "so a level with custom animation sets or new animation contexts may need it. Every other level keeps the player's own choice.");
            tips.SetToolTip(playlistOn, "A movie or image playlist from DATA\\GBL_ITEM.BML (Configuration Editors > Core Game > Loadscreen Movies edits them). " +
                                        "It plays as the level loads from a launch, from the main menu's level select and from a checkpoint in another level.");

            saveBtn.Click += (s, e) => Save();
            closeBtn.Click += (s, e) => Close();
            heapOn.CheckedChanged += (s, e) => Changed();
            heapMB.ValueChanged += (s, e) => Changed();
            npcCollide.CheckedChanged += (s, e) => Changed();
            playerCollide.CheckedChanged += (s, e) => Changed();
            playlistOn.CheckedChanged += (s, e) => Changed();
            playlist.TextChanged += (s, e) => Changed();
            animOn.CheckedChanged += (s, e) => Changed();

            buttons.Controls.AddRange(new Control[] { closeBtn, saveBtn });
            Controls.Add(body);
            Controls.Add(status);
            Controls.Add(buttons);
            Controls.Add(intro);

            AcceptButton = saveBtn;
            CancelButton = closeBtn;
            Load += (s, e) => Fill();
            FormClosed += (s, e) => tips.Dispose();
        }

        private static Label Heading(string text) => new Label { AutoSize = true, Text = text, Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold), Padding = new Padding(0, 10, 0, 2) };

        private static FlowLayoutPanel Row(params Control[] controls)
        {
            FlowLayoutPanel row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            row.Controls.AddRange(controls);
            return row;
        }

        private void Fill()
        {
            _filling = true;
            try
            {
                string pathToAI = Singleton.PathToAI;
                LevelLaunchSettings settings = LevelLaunchSettings.Of(pathToAI, _level);
                heapOn.Checked = settings.TextureHeapMB != null;
                if (settings.TextureHeapMB != null)
                    heapMB.Value = Math.Max(heapMB.Minimum, Math.Min(heapMB.Maximum, settings.TextureHeapMB.Value));
                npcCollide.Checked = settings.NpcCollisions != false;
                playerCollide.Checked = settings.PlayerNpcCollisions != false;
                animOn.Checked = settings.LargerAnimationMemory == true;

                playlist.Items.Clear();
                foreach (string name in LevelLaunchSettings.Playlists(pathToAI).OrderBy(o => o, StringComparer.OrdinalIgnoreCase))
                    playlist.Items.Add(name);
                playlistOn.Checked = !string.IsNullOrWhiteSpace(settings.LoadingPlaylist) && !settings.HasMovies;
                movies.Items.Clear();
                foreach (string movie in settings.LoadingMovies ?? new List<string>())
                    if (!string.IsNullOrWhiteSpace(movie)) movies.Items.Add(movie);
                moviesOn.Checked = settings.HasMovies;
                moviesToEnd.Checked = settings.LoadingMoviesToEnd == true;
                playlist.Text = settings.LoadingPlaylist ?? LevelLaunchSettings.GamePlaylist(_level);

                (int? def, int? ultra) = TextureBudget.ReadHeap(System.IO.File.Exists(TextureBudget.SettingsPath(pathToAI)) ? System.IO.File.ReadAllText(TextureBudget.SettingsPath(pathToAI)) : "");
                int own = ultra ?? def ?? TextureBudget.RetailHeapMB;
                heapNote.Text = "The player's own heap is " + own + " MB (DATA\\ENGINE_SETTINGS.XML); a launch into this level uses the larger of the two. " +
                                "The game is 32-bit and reserves the heap from its own memory as it starts, so a level changed to inside the game keeps the heap it started with. " +
                                "Level Check's Texture memory estimates what the level needs.";
                playlistNote.Text = "As the game ships, this level loads with " + LevelLaunchSettings.GamePlaylist(_level) + ". A level's own plays as it loads from a launch, " +
                                    "from the main menu's level select and from a checkpoint in another level; a SwitchLevel into it plays what the script last set " +
                                    "(SetNextLoadingMovie), and a restart after death plays the loading screen's usual footage, as in the game.";
            }
            finally
            {
                _filling = false;
            }
            Changed(dirty: false);
        }

        private LevelLaunchSettings FromControls() => new LevelLaunchSettings()
        {
            TextureHeapMB = heapOn.Checked ? (int)heapMB.Value : (int?)null,
            NpcCollisions = npcCollide.Checked ? (bool?)null : false,
            PlayerNpcCollisions = playerCollide.Checked ? (bool?)null : false,
            LoadingPlaylist = playlistOn.Checked && !string.IsNullOrWhiteSpace(playlist.Text) ? playlist.Text.Trim() : null,
            LargerAnimationMemory = animOn.Checked ? true : (bool?)null,
            LoadingMovies = moviesOn.Checked && movies.Items.Count != 0 ? movies.Items.Cast<string>().ToList() : null,
            LoadingMoviesToEnd = moviesOn.Checked && moviesToEnd.Checked ? true : (bool?)null,
        };

        /* A .usm copied into the level's MOVIES folder and put on the list */
        private void AddMovie()
        {
            using (OpenFileDialog dialog = new OpenFileDialog() { Title = "A loading movie for " + _level, Filter = "Movies the game plays (*.usm)|*.usm|All files (*.*)|*.*" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    string name = LevelLoadingMovies.Add(Singleton.PathToAI, _level, dialog.FileName);
                    if (!movies.Items.Cast<string>().Contains(name, StringComparer.OrdinalIgnoreCase))
                        movies.Items.Add(name);
                    moviesOn.Checked = true;
                    Changed();
                }
                catch (Exception e)
                {
                    MessageBox.Show("The movie could not be added: " + e.Message + ".", "Level Launch Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void Changed(bool dirty = true)
        {
            if (_filling)
                return;
            heapMB.Enabled = heapOn.Checked;
            playlist.Enabled = playlistOn.Checked;
            movies.Enabled = removeMovieBtn.Enabled = moviesToEnd.Enabled = moviesOn.Checked;
            saveBtn.Enabled = dirty;
            LevelLaunchSettings settings = FromControls();
            List<string> notes = settings.Problems(Singleton.PathToAI);
            notes.AddRange(settings.Warnings(Singleton.PathToAI, _level));
            string inUse = LevelLaunch.InUseFor(Singleton.PathToAI);
            string now = inUse == null ? "AI.exe holds no level's settings now." : "AI.exe holds " + (inUse == "" ? "a level's" : inUse + "'s") + " settings now (until the game closes).";
            status.Text = (notes.Count != 0 ? string.Join(". ", notes) + ". " : "") + now;
        }

        private void Save()
        {
            LevelLaunchSettings settings = FromControls();
            string problem = LevelLaunchSettings.Save(Singleton.PathToAI, _level, settings);
            if (problem != null)
            {
                MessageBox.Show("The settings could not be saved: " + problem + ".", "Level Launch Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Changed(dirty: false);
        }
    }
}

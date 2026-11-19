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
    public partial class LevelLaunchSettingsEditor : BaseWindow
    {
        private readonly string _level;
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
            InitializeComponent();
            _level = level.Replace('\\', '/');

            Text = "Level Launch Settings - " + _level;
            Icon = SharedFormIcon.Icon;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/level-launch-settings/");

            heapMB.Minimum = LevelLaunchSettings.MinHeapMB;
            heapMB.Maximum = LevelLaunchSettings.MaxHeapMB;
            heapMB.Value = 512;
            //Wired after the limits and starting value are set, which are not edits
            heapMB.ValueChanged += Setting_Changed;

            capNote.Text = "The game holds " + CharacterCapRule.Pool + " characters at once, the player and every corpse not yet despawned included. " +
                           "That can't be raised: the game sizes its character, AI and animation tables to it. OpenCAGE's launches stop the crash that spawning past it causes: " +
                           "a spawn with no free slot waits (despawning the corpse furthest out of view, as the game does), and characters spawning in the same moment that find none left are left out. " +
                           "Level Check warns when a level can reach it.";
        }

        private void LevelLaunchSettingsEditor_Load(object sender, EventArgs e) => Fill();
        private void LevelLaunchSettingsEditor_FormClosed(object sender, FormClosedEventArgs e) => tips.Dispose();

        private void Setting_Changed(object sender, EventArgs e) => Changed();

        //One or the other: a playlist of the game's, or the level's own movies
        private void playlistOn_CheckedChanged(object sender, EventArgs e)
        {
            if (playlistOn.Checked) moviesOn.Checked = false;
            Changed();
        }

        private void moviesOn_CheckedChanged(object sender, EventArgs e)
        {
            if (moviesOn.Checked) playlistOn.Checked = false;
            Changed();
        }

        private void addMovieBtn_Click(object sender, EventArgs e) => AddMovie();

        private void removeMovieBtn_Click(object sender, EventArgs e)
        {
            if (movies.SelectedIndex < 0) return;
            movies.Items.RemoveAt(movies.SelectedIndex);
            Changed();
        }

        private void saveBtn_Click(object sender, EventArgs e) => Save();
        private void closeBtn_Click(object sender, EventArgs e) => Close();

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

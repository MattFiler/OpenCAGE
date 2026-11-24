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
    /// playlist. Used from the next launch into the level; everything else keeps the game's own. Also the name its saves show
    /// (LevelSaveName), which is the level's own data rather than a launch setting.
    /// </summary>
    public partial class LevelLaunchSettingsEditor : BaseWindow
    {
        private readonly string _level;
        private bool _filling;
        //What names the level's saves, as last read, and the name the field started from
        private LevelSaveName.State _save = new LevelSaveName.State();
        private string _savedName = "";
        //How many save files of its own the level has (read as the window fills)
        private int _ownSaveFiles;

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
                ReadSaveName();
                ownSaves.Checked = LevelSaveSlots.Own(_level, settings);
                ownSaves.Enabled = !EditorUtils.IsFrontend(_level);
                _ownSaveFiles = LevelSaveSlots.Find(Singleton.Platform, _level).Count;

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
            //Saved as the level's default when it is (LevelLaunchSettings.Save leaves the default out of the file)
            OwnSaves = ownSaves.Enabled ? ownSaves.Checked : (bool?)null,
        };

        /* What keeping the level's saves apart does, as the box reads now */
        private string OwnSavesNote()
        {
            if (EditorUtils.IsFrontend(_level))
                return "The main menu makes no saves.";
            if (!ownSaves.Checked)
                return "A save made in this level takes the place of the campaign's older checkpoint and becomes the one Continue loads, as the game does: two saves here leave none of the campaign's checkpoints (its mission saves stay)." +
                       (LevelSaveSlots.DefaultFor(_level) ? "" : " This is one of the game's own levels: its saves are the campaign's.");
            string[] names = LevelSaveSlots.NamesFor(LevelSaveSlots.TagFor(_level));
            return "A launch into this level from OpenCAGE gives it save files of its own - " + names[0] + "00 and " + names[0] + "01 (its checkpoints), " + names[1] + "<mission> and " + names[2] +
                   " in the game's save folder - so its saves never take the place of the campaign's, and Continue and Load Game show only the level's own while it is played. " +
                   "The game's options kept with saves (gamma, subtitles, invert, mouse speed) start at their defaults the first time, then stay with the level. The game started any other way uses the campaign's saves. " +
                   (_ownSaveFiles == 0 ? "The level has no saves of its own yet." : "The level has " + _ownSaveFiles + " save file" + (_ownSaveFiles == 1 ? "" : "s") + " of its own now.");
        }

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

        /* The field from what names the level's saves now: editable only for the level open, whose script holds it */
        private void ReadSaveName()
        {
            _save = LevelSaveName.Read(Singleton.PathToAI, _level);
            _savedName = _save.Text ?? "";
            saveName.Text = _savedName;
            saveName.Enabled = _save.ScriptRead && !EditorUtils.IsFrontend(_level);
        }

        /* What the save name does, as the field reads now */
        private string SaveNote()
        {
            if (EditorUtils.IsFrontend(_level))
                return "The main menu makes no saves.";
            if (!_save.ScriptRead)
                return "Open the level to see or change the name its saves show: it is kept in the level's script.";
            string name = saveName.Text.Trim();
            string note = name.Length == 0
                ? "Without one, a save made in this level shows \"Unknown Level\" on the load screen's save list (or, after another level in the same game, that level's name). "
                : "A save made in this level shows as \"" + LevelSaveName.Shown(name, 1, 1) + "\" on the load screen's save list. ";
            if (name == _savedName && _save.Id != null && _save.Text == null)
                note += "The level's script names " + _save.Id + ", a string none of its text databases holds, so its saves show \"Unknown Level\". ";
            else if (name == _savedName && _save.Id != null && !_save.Own)
                note += "The level's script names it itself (" + _save.Id + "). ";
            if (_save.Mixed)
                note += "Its LevelInfo entities name different strings: saving a name here gives them all the same one. ";
            note += "The name is kept in the level itself, so it shows however the game is started: a LevelInfo entity in the level's script names a string in the level's own text database " +
                    LevelSaveName.Database + " (the same text in every language). Saving it here changes the script as one undo step: save the level to keep it. A save keeps the name it was made with.";
            return note;
        }

        private void Changed(bool dirty = true)
        {
            if (_filling)
                return;
            heapMB.Enabled = heapOn.Checked;
            playlist.Enabled = playlistOn.Checked;
            movies.Enabled = removeMovieBtn.Enabled = moviesToEnd.Enabled = moviesOn.Checked;
            saveBtn.Enabled = dirty;
            saveNote.Text = SaveNote();
            ownSavesNote.Text = OwnSavesNote();
            LevelLaunchSettings settings = FromControls();
            List<string> notes = settings.Problems(Singleton.PathToAI);
            string nameProblem = saveName.Enabled ? LevelSaveName.Problem(saveName.Text) : null;
            if (nameProblem != null)
                notes.Add(nameProblem);
            notes.AddRange(settings.Warnings(Singleton.PathToAI, _level));
            string inUse = LevelLaunch.InUseFor(Singleton.PathToAI);
            string now = inUse == null ? "AI.exe holds no level's settings now." : "AI.exe holds " + (inUse == "" ? "a level's" : inUse + "'s") + " settings now (until the game closes).";
            status.Text = (notes.Count != 0 ? string.Join(". ", notes) + ". " : "") + now;
        }

        private void Save()
        {
            LevelLaunchSettings settings = FromControls();
            bool nameChanged = saveName.Enabled && saveName.Text.Trim() != _savedName;
            string problem = (nameChanged ? LevelSaveName.Problem(saveName.Text) : null) ?? LevelLaunchSettings.Save(Singleton.PathToAI, _level, settings);
            if (problem != null)
            {
                MessageBox.Show("The settings could not be saved: " + problem + ".", "Level Launch Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            //The save name is the level's own data: its text database, and its script as one undo step
            if (nameChanged)
            {
                try
                {
                    LevelSaveName.Set(Singleton.PathToAI, _level, saveName.Text, false);
                }
                catch (Exception e)
                {
                    MessageBox.Show("The save name could not be set: " + e.Message, "Level Launch Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _filling = true;
                try { ReadSaveName(); }
                finally { _filling = false; }
            }
            Changed(dirty: false);
        }
    }
}

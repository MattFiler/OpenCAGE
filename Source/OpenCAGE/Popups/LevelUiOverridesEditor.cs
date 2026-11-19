using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// A level's own UI movies (LevelUiOverrides): every movie the game opens by name, and whether the level replaces it with a .gfx
    /// in its UI folder. Changes apply from the next launch into the level.
    /// </summary>
    public partial class LevelUiOverridesEditor : BaseWindow
    {
        private readonly string _level;

        /// <summary>Show a level's UI overrides (one window per level: an open one comes to the front).</summary>
        public static void ShowFor(IWin32Window owner, string level)
        {
            if (string.IsNullOrWhiteSpace(level))
            {
                MessageBox.Show("Open a level (or pick one in Launch Game) to see its UI overrides.", "Level UI Overrides", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            LevelUiOverridesEditor open = Application.OpenForms.OfType<LevelUiOverridesEditor>().FirstOrDefault(o => string.Equals(o._level, level, StringComparison.OrdinalIgnoreCase));
            if (open != null)
            {
                open.Activate();
                return;
            }
            LevelUiOverridesEditor editor = new LevelUiOverridesEditor(level);
            if (owner is Form form && form.TopMost)
                editor.TopMost = true;
            editor.Show(owner);
        }

        public LevelUiOverridesEditor(string level)
        {
            InitializeComponent();
            _level = level.Replace('\\', '/');

            Text = "Level UI Overrides - " + _level;
            Icon = SharedFormIcon.Icon;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/ui-editor/#level-ui-overrides");
        }

        private void LevelUiOverridesEditor_Load(object sender, EventArgs e) => Fill();
        private void LevelUiOverridesEditor_Activated(object sender, EventArgs e) => Fill();
        private void LevelUiOverridesEditor_FormClosed(object sender, FormClosedEventArgs e) => tips.Dispose();

        private void importBtn_Click(object sender, EventArgs e) => Import();
        private void exportGameBtn_Click(object sender, EventArgs e) => Export(game: true);
        private void exportBtn_Click(object sender, EventArgs e) => Export(game: false);
        private void removeBtn_Click(object sender, EventArgs e) => Remove();
        private void folderBtn_Click(object sender, EventArgs e) => OpenFolder();
        private void closeBtn_Click(object sender, EventArgs e) => Close();

        private void list_SelectedIndexChanged(object sender, EventArgs e) => UpdateButtons();
        private void list_DoubleClick(object sender, EventArgs e) => Import();

        private void list_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Delete) return;
            e.Handled = true;
            Remove();
        }

        private LevelUiOverrides.Movie SelectedMovie => list.SelectedItems.Count == 0 ? null : list.SelectedItems[0].Tag as LevelUiOverrides.Movie;

        /* The list as the level's folder has it now */
        private void Fill()
        {
            string pathToAI = Singleton.PathToAI;
            LevelUiOverrides.Movie selected = SelectedMovie;
            Dictionary<LevelUiOverrides.Movie, string> own = LevelUiOverrides.Of(pathToAI, _level);
            List<LevelUiOverrides.Movie> inUse = LevelUiOverrides.InUse(pathToAI);
            bool inUseHere = string.Equals(LevelUiOverrides.InUseFor(pathToAI), _level, StringComparison.OrdinalIgnoreCase);

            list.BeginUpdate();
            list.Items.Clear();
            foreach (LevelUiOverrides.Movie movie in LevelUiOverrides.All)
            {
                string state = "The game's own";
                if (own.TryGetValue(movie, out string file))
                {
                    FileInfo info = new FileInfo(file);
                    state = "Its own (" + Math.Max(1, info.Length / 1024) + " KB, " + info.LastWriteTime.ToString("d MMM yyyy HH:mm") + ")";
                    if (movie.Tweak != null && UiMovieOverrides.IsOn(movie.Tweak))
                        state += " - used instead of " + LevelUiOverrides.TweakName(movie.Tweak);
                    if (inUse.Contains(movie) && inUseHere)
                        state += " - in the game now";
                }
                ListViewItem item = new ListViewItem(new[] { movie.Label, movie.FileName, state }) { Tag = movie };
                item.ToolTipText = movie.Label + "\nThe game opens it as " + movie.GamePath + "; a launch into this level points it at " + movie.LevelPath + ".";
                if (file != null)
                    item.Font = new Font(list.Font, FontStyle.Bold);
                list.Items.Add(item);
                if (movie == selected)
                    item.Selected = true;
            }
            list.EndUpdate();

            string folder = LevelUiOverrides.Folder(pathToAI, _level);
            string text = _level + ": " + (own.Count == 0 ? "no UI movies of its own yet." : own.Count + " UI movie" + (own.Count == 1 ? "" : "s") + " of its own.") +
                "\nKept in " + Relative(folder) + ". A launch into the level from OpenCAGE (Launch Game, Launch Mod) uses them; they come out again when the game closes or anything else is launched.";
            List<string> unused = LevelUiOverrides.Unused(pathToAI, _level);
            if (unused.Count != 0)
                text += "\nNot used (not the name of one of the game's movies): " + string.Join(", ", unused.Select(Path.GetFileName)) + ".";
            summary.Text = text;
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            LevelUiOverrides.Movie movie = SelectedMovie;
            bool own = movie != null && LevelUiOverrides.Of(Singleton.PathToAI, _level).ContainsKey(movie);
            importBtn.Enabled = movie != null;
            exportGameBtn.Enabled = movie != null;
            exportBtn.Enabled = own;
            removeBtn.Enabled = own;
        }

        private static string Relative(string path)
        {
            string root = Path.GetFullPath(Singleton.PathToAI ?? "");
            string full = Path.GetFullPath(path);
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length).TrimStart('\\', '/') : full;
        }

        private void Import()
        {
            LevelUiOverrides.Movie movie = SelectedMovie;
            if (movie == null)
                return;
            using (OpenFileDialog dialog = new OpenFileDialog() { Title = "The level's own " + movie.Label, Filter = "Scaleform movies (*.gfx;*.swf)|*.gfx;*.swf|All files (*.*)|*.*" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                string problem = LevelUiOverrides.Set(Singleton.PathToAI, _level, movie, dialog.FileName);
                if (problem != null)
                    MessageBox.Show("The movie could not be added: " + problem + ".", "Level UI Overrides", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            Fill();
        }

        private void Export(bool game)
        {
            LevelUiOverrides.Movie movie = SelectedMovie;
            if (movie == null)
                return;
            byte[] bytes = null;
            try
            {
                if (game)
                    bytes = LevelUiOverrides.GameVersion(Singleton.PathToAI, movie);
                else if (LevelUiOverrides.Of(Singleton.PathToAI, _level).TryGetValue(movie, out string file))
                    bytes = File.ReadAllBytes(file);
            }
            catch { }
            if (bytes == null)
            {
                MessageBox.Show(game ? "UI.PAK has no " + movie.OriginalEntry + " (check the game's files)." : "The level has no " + movie.FileName + " of its own.", "Level UI Overrides", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using (SaveFileDialog dialog = new SaveFileDialog() { Title = (game ? "The game's " : "The level's ") + movie.Label, FileName = movie.FileName, Filter = "Scaleform movie (*.gfx)|*.gfx|All files (*.*)|*.*" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    File.WriteAllBytes(dialog.FileName, bytes);
                }
                catch (Exception e)
                {
                    MessageBox.Show("The movie could not be saved: " + e.Message, "Level UI Overrides", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void Remove()
        {
            LevelUiOverrides.Movie movie = SelectedMovie;
            if (movie == null || !LevelUiOverrides.Of(Singleton.PathToAI, _level).ContainsKey(movie))
                return;
            string problem = LevelUiOverrides.Set(Singleton.PathToAI, _level, movie, null);
            if (problem != null)
                MessageBox.Show("The movie could not be taken out: " + problem + ".", "Level UI Overrides", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Fill();
        }

        private void OpenFolder()
        {
            string folder = LevelUiOverrides.Folder(Singleton.PathToAI, _level);
            try
            {
                if (!Directory.Exists(folder))
                    folder = Path.GetDirectoryName(folder);
                Process.Start("explorer.exe", "\"" + folder + "\"");
            }
            catch { }
        }
    }
}

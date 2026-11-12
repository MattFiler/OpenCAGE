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
    public class LevelUiOverridesEditor : BaseWindow
    {
        private readonly string _level;

        private readonly Label summary = new Label { Dock = DockStyle.Top, Height = 66, Padding = new Padding(6, 6, 6, 0) };
        private readonly ListView list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, ShowItemToolTips = true };
        private readonly FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(4) };
        private readonly Button closeBtn = new Button { Text = "Close", Width = 90, Height = 26 };
        private readonly Button removeBtn = new Button { Text = "Remove", Width = 90, Height = 26 };
        private readonly Button exportBtn = new Button { Text = "Export...", Width = 90, Height = 26 };
        private readonly Button exportGameBtn = new Button { Text = "Export Game's...", Width = 115, Height = 26 };
        private readonly Button importBtn = new Button { Text = "Import...", Width = 90, Height = 26 };
        private readonly Button folderBtn = new Button { Text = "Open Folder", Width = 100, Height = 26 };
        private readonly ToolTip tips = new ToolTip() { AutoPopDelay = 15000 };

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
            _level = level.Replace('\\', '/');

            Text = "Level UI Overrides - " + _level;
            Icon = SharedFormIcon.Icon;
            ClientSize = new Size(860, 520);
            MinimumSize = new Size(560, 300);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/ui-editor/#level-ui-overrides");

            list.Columns.Add("Movie", 280);
            list.Columns.Add("File", 230);
            list.Columns.Add("On this level", 320);

            tips.SetToolTip(importBtn, "Give the level its own version of the selected movie: a .gfx (or the .swf it is made from), copied into the level's UI folder. Used from the next launch into the level.");
            tips.SetToolTip(exportGameBtn, "Save the game's own version of the selected movie, from UI.PAK - a starting point for the level's own.");
            tips.SetToolTip(exportBtn, "Save the level's own version of the selected movie.");
            tips.SetToolTip(removeBtn, "Take the level's own version of the selected movie out: the game's is used again.");
            tips.SetToolTip(folderBtn, "Open the level's UI folder (DATA\\ENV\\<level>\\UI), where its movies are kept under the game's file names.");
            importBtn.Click += (s, e) => Import();
            exportGameBtn.Click += (s, e) => Export(game: true);
            exportBtn.Click += (s, e) => Export(game: false);
            removeBtn.Click += (s, e) => Remove();
            folderBtn.Click += (s, e) => OpenFolder();
            closeBtn.Click += (s, e) => Close();
            list.SelectedIndexChanged += (s, e) => UpdateButtons();
            list.DoubleClick += (s, e) => Import();
            list.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Delete) return;
                e.Handled = true;
                Remove();
            };

            //Right to left: Close at the far right, then what acts on the selection
            buttons.Controls.AddRange(new Control[] { closeBtn, removeBtn, exportBtn, exportGameBtn, importBtn, folderBtn });

            Controls.Add(list);
            Controls.Add(buttons);
            Controls.Add(summary);

            CancelButton = closeBtn;
            Load += (s, e) => Fill();
            Activated += (s, e) => Fill();
            FormClosed += (s, e) => tips.Dispose();
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

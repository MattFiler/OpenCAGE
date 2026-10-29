using CATHODE;
using CATHODE.Animations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.AnimTrees
{
    public partial class AnimationSets : DockContent
    {
        private List<AnimationTreeGraph> _graphs = new List<AnimationTreeGraph>();
        private List<(AnimTreeDB Database, PAK2.File PakEntry)> _animTreeDbs = new List<(AnimTreeDB, PAK2.File)>();
        private AnimTreeDB _selectedDb = null;
        private bool _suppressSearchChanged = false;

        public AnimationSets()
        {
            InitializeComponent();
            Theming.ThemeManager.ApplyToForm(this);
            CloseButton = false;
            CloseButtonVisible = false;

            foreach (AnimTreeDB db in Singleton.Global.Animations.Trees)
            {
                PAK2.File file = Singleton.Global.Animations.PAK.Entries.FirstOrDefault(o => string.Equals(o.Filename, db.Filepath, StringComparison.OrdinalIgnoreCase));
                if (file == null) continue;

                _animTreeDbs.Add((db, file));
#if DEBUG
                /*
                File.WriteAllText(Path.GetFileName(file.Filename) + ".json", JsonConvert.SerializeObject(db, new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                }));
                */
#endif
            }

            animSets.BeginUpdate();
            foreach (var entry in _animTreeDbs.OrderBy(o => o.Database.Set))
                animSets.Items.Add(new ListViewItem(entry.Database.Entries[0].Set) { Tag = entry.Database });
            animSets.EndUpdate();

            ResizeListColumns();
            animSets.SizeChanged += (s, e) => ResizeListColumns();
            animTrees.SizeChanged += (s, e) => ResizeListColumns();

#if DEBUG
            //test code: loads PERSISTENT_ACT_GUN_LAYER_FP within HUMANOID
            animSets.Items[3].Selected = true;
#endif
        }

        private void ResizeListColumns()
        {
            if (animSets.Columns.Count > 0)
                animSets.Columns[0].Width = Math.Max(50, animSets.ClientSize.Width - 4);
            if (animTrees.Columns.Count > 0)
                animTrees.Columns[0].Width = Math.Max(50, animTrees.ClientSize.Width - 4);
        }

        /// <summary>A value half-typed into an open tree's inspector goes into the tree.</summary>
        public void CommitPendingEdits()
        {
            foreach (AnimationTreeGraph graph in _graphs)
                graph.CommitPendingEdits();
        }

        /// <summary>
        /// The tree sets whose trees in memory aren't what ANIMATION.PAK holds - their entries in the PAK in memory, which are
        /// the file as it was last loaded or written: changed, and not saved.
        /// </summary>
        internal static List<AnimTreeDB> ChangedSets(CathodeLib.Animation animations)
        {
            List<AnimTreeDB> changed = new List<AnimTreeDB>();
            if (animations?.PAK == null || !animations.ContentsLoaded)
                return changed;
            Dictionary<string, PAK2.File> entries = EntriesByName(animations);
            foreach (AnimTreeDB database in animations.Trees)
            {
                entries.TryGetValue(database.Filepath ?? "", out PAK2.File entry);
                byte[] content = null;
                try { content = database.ToBytes(); }
                catch { }
                if (entry?.Content == null || content == null || !entry.Content.AsSpan().SequenceEqual(content))
                    changed.Add(database);
            }
            return changed;
        }

        /// <summary>
        /// Let go of tree changes that weren't saved: each of these tree sets is read again from its entry in the PAK in memory
        /// (what the file holds) and takes its place in the animations, so no other write of ANIMATION.PAK can take the changes
        /// out. The layouts waiting to be written go too, and the nodes copied from the trees as they were.
        /// </summary>
        internal static void LetGo(CathodeLib.Animation animations, List<AnimTreeDB> changed)
        {
            if (animations?.PAK == null)
                return;
            Dictionary<string, PAK2.File> entries = EntriesByName(animations);
            foreach (AnimTreeDB database in changed)
            {
                if (!entries.TryGetValue(database.Filepath ?? "", out PAK2.File entry) || entry.Content == null)
                    continue;
                AnimTreeDB saved = null;
                try { saved = new AnimTreeDB(entry.Content, animations.StringsDebug, database.Filepath); }
                catch { }
                if (saved == null || !saved.Loaded)
                    continue;
                int at = animations.Trees.IndexOf(database);
                if (at >= 0)
                    animations.Trees[at] = saved;
            }
            AnimTreeLayoutManager.Reset();
            AnimTreeClipboard.Clear();
        }

        private static Dictionary<string, PAK2.File> EntriesByName(CathodeLib.Animation animations)
        {
            Dictionary<string, PAK2.File> entries = new Dictionary<string, PAK2.File>(StringComparer.OrdinalIgnoreCase);
            foreach (PAK2.File entry in animations.PAK.Entries)
                if (entry?.Filename != null && !entries.ContainsKey(entry.Filename))
                    entries[entry.Filename] = entry;
            return entries;
        }

        public bool SaveAll()
        {
            // Commit any in-progress property grid edits before serialising
            CommitPendingEdits();

            CathodeLib.Animation animations = Singleton.Global.Animations;
            string error, layoutError = null;
            bool saved = false, layoutsIn = false;
            //Shown while it writes, and closed before anything is said about it (it would cover the message)
            using (AnimationPakWrite.WriteProgress progress = new AnimationPakWrite.WriteProgress(animations, "Saving the animation trees", FindForm()))
            {
                //The file still the one loaded, and every set checked to load again with every name it uses registered - all of
                //it before any goes into the PAK, so a refused save leaves nothing half-written for another write to take out
                progress.Step("Checking the animation trees...", 0.0);
                error = AnimationPakWrite.Stale(animations);
                List<(AnimTreeDB Database, PAK2.File Entry, byte[] Content)> sets = error == null ? AnimationPakWrite.PrepareTrees(animations, out error) : null;
                if (error == null)
                {
                    //The debug string table every time: names made here (by this save, or one refused before it) read back as names
                    if (animations.StringsDebug != null)
                    {
                        PAK2.File strings = animations.PAK.Entries.FirstOrDefault(o => string.Equals(o.Filename, animations.StringsDebug.Filepath, StringComparison.OrdinalIgnoreCase));
                        if (strings != null)
                            strings.Content = animations.StringsDebug.ToBytes();
                    }
                    foreach (var (database, pakEntry, content) in sets)
                        pakEntry.Content = content;

                    //Where the trees' nodes are drawn goes into ANIMATION.PAK with them
                    layoutsIn = AnimTreeLayoutManager.Commit(out layoutError);

                    progress.KeepingOriginal(0.02);
                    OpenCAGE.Modding.ModServices.CaptureBeforeWrite(animations.PAK.Filepath);
                    progress.Writing(0.05, false);
                    try { saved = animations.PAK.Save(); }
                    finally { AnimationPakWrite.Written(animations, saved, false); }
                }
            }
            if (error != null)
            {
                MessageBox.Show(error, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if (!saved)
            {
                MessageBox.Show(
                    "Failed to write ANIMATION.PAK.",
                    "Save failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }

            if (!layoutsIn)
            {
                MessageBox.Show(
                    "The animation trees were saved, but their node layouts were not: " + layoutError + ".",
                    "Layouts not saved",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private void animSets_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (animSets.SelectedItems == null || animSets.SelectedItems.Count == 0)
                return;

            _selectedDb = (AnimTreeDB)animSets.SelectedItems[0].Tag;

            _suppressSearchChanged = true;
            treeSearchBox.Text = "";
            _suppressSearchChanged = false;

            PopulateTreesList();

#if DEBUG
            //test code: loads PERSISTENT_ACT_GUN_LAYER_FP within HUMANOID
            if (animSets.SelectedIndices.Count > 0 && animSets.SelectedIndices[0] == 3 && animTrees.Items.Count > 79)
                animTrees.Items[79].Selected = true;
#endif
        }

        private void treeSearchBox_TextChanged(object sender, EventArgs e)
        {
            if (_suppressSearchChanged || _selectedDb == null)
                return;

            PopulateTreesList();
        }

        private void PopulateTreesList()
        {
            if (_selectedDb == null)
            {
                animTrees.Items.Clear();
                return;
            }

            string filter = (treeSearchBox.Text ?? "").Trim();
            IEnumerable<AnimationTree> trees = _selectedDb.Entries.OrderBy(o => o.Name);
            if (!string.IsNullOrEmpty(filter))
                trees = trees.Where(t => t.Name != null && t.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

            animTrees.BeginUpdate();
            animTrees.Items.Clear();
            foreach (AnimationTree tree in trees)
                animTrees.Items.Add(new ListViewItem(tree.Name) { Tag = tree });
            animTrees.EndUpdate();
            ResizeListColumns();
        }

        private void animTrees_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (animTrees.SelectedItems == null || animTrees.SelectedItems.Count == 0)
                return;

            //temp: only allow one graph
            AnimationTreeGraph[] graphs = _graphs.ToArray();
            for (int i = 0; i < graphs.Length; i++)
                graphs[i].Close();

            AnimationTreeGraph graph = new AnimationTreeGraph();
            graph.PopulateGraph(_selectedDb, (AnimationTree)animTrees.SelectedItems[0].Tag);
            graph.FormClosed += Graph_FormClosed;
            _graphs.Add(graph);

            graph.Show(AnimTreeEditor.DockPanel, DockState.Document);
        }

        /// <summary>Show a tree as if it were picked from the lists: its set and the tree selected there, its graph open. Returns the graph.</summary>
        internal AnimationTreeGraph OpenTree(AnimTreeDB database, AnimationTree tree)
        {
            AnimationTreeGraph open = _graphs.FirstOrDefault(o => ReferenceEquals(o.Tree, tree));
            if (open != null)
            {
                open.Activate();
                return open;
            }

            ListViewItem set = animSets.Items.Cast<ListViewItem>().FirstOrDefault(o => ReferenceEquals(o.Tag, database));
            if (set == null)
                return null;
            if (!set.Selected)
            {
                animSets.SelectedItems.Clear();
                set.Selected = true;
            }
            if (!ReferenceEquals(_selectedDb, database) || treeSearchBox.Text != "")
            {
                _selectedDb = database;
                _suppressSearchChanged = true;
                treeSearchBox.Text = "";
                _suppressSearchChanged = false;
                PopulateTreesList();
            }
            set.EnsureVisible();

            ListViewItem item = animTrees.Items.Cast<ListViewItem>().FirstOrDefault(o => ReferenceEquals(o.Tag, tree));
            if (item == null)
                return null;
            animTrees.SelectedItems.Clear();
            item.Selected = true;
            item.EnsureVisible();
            return _graphs.FirstOrDefault(o => ReferenceEquals(o.Tree, tree));
        }

        private void Graph_FormClosed(object sender, FormClosedEventArgs e)
        {
            AnimationTreeGraph graph = (AnimationTreeGraph)sender;
            graph.FormClosed -= Graph_FormClosed;
            _graphs.Remove(graph);
        }
    }
}

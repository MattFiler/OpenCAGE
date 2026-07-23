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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TreeView;

namespace OpenCAGE.AnimTrees
{
    public partial class AnimationSets : DockContent
    {
        private List<AnimationTreeGraph> _graphs = new List<AnimationTreeGraph>();
        private List<(AnimTreeDB Database, PAK2.File PakEntry)> _animTreeDbs = new List<(AnimTreeDB, PAK2.File)>();

        public AnimationSets()
        {
            InitializeComponent();

            List<PAK2.File> files = Singleton.Global.Animations.Entries.FindAll(o => o.Filename.ToUpper().Contains("ANIM_TREE_DB"));
            foreach (PAK2.File file in files)
            {
                AnimTreeDB db = new AnimTreeDB(file.Content, Singleton.AnimationStrings_Debug, file.Filename);
                _animTreeDbs.Add((db, file));
#if DEBUG
                File.WriteAllText(Path.GetFileName(file.Filename) + ".json", JsonConvert.SerializeObject(db, Newtonsoft.Json.Formatting.Indented));
#endif
            }

            animSets.BeginUpdate();
            foreach (var entry in _animTreeDbs.OrderBy(o => o.Database.Set))
                animSets.Items.Add(new ListViewItem(entry.Database.Entries[0].Set) { Tag = entry.Database });
            animSets.EndUpdate();

#if DEBUG
            //test code: loads PERSISTENT_ACT_GUN_LAYER_FP within HUMANOID
            animSets.Items[3].Selected = true;
#endif
        }

        public bool SaveAll()
        {
            // Commit any in-progress property grid edits before serialising
            foreach (AnimationTreeGraph graph in _graphs)
                graph.CommitPendingEdits();

            foreach (var (database, pakEntry) in _animTreeDbs)
            {
                string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bin");
                try
                {
                    if (!database.Save(tempPath, false))
                    {
                        MessageBox.Show(
                            "Failed to serialise animation set '" + database.Set + "'.",
                            "Save failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return false;
                    }
                    pakEntry.Content = File.ReadAllBytes(tempPath);
                }
                finally
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
            }

            if (!Singleton.Global.Animations.Save())
            {
                MessageBox.Show(
                    "Failed to write ANIMATION.PAK.",
                    "Save failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        private void animSets_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (animSets.SelectedItems == null || animSets.SelectedItems.Count == 0) return;

            animTrees.BeginUpdate();
            AnimTreeDB db = (AnimTreeDB)animSets.SelectedItems[0].Tag;
            animTrees.Items.Clear();
            foreach (AnimationTree tree in db.Entries.OrderBy(o => o.Name))
                animTrees.Items.Add(new ListViewItem(tree.Name) { Tag = tree });
            animTrees.EndUpdate();

#if DEBUG
            //test code: loads PERSISTENT_ACT_GUN_LAYER_FP within HUMANOID
            if (animSets.SelectedIndices.Count > 0 && animSets.SelectedIndices[0] == 3)
                animTrees.Items[79].Selected = true;
#endif
        }

        private void animTrees_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (animTrees.SelectedItems == null || animTrees.SelectedItems.Count == 0) return;

            //temp: only allow one graph
            AnimationTreeGraph[] graphs = _graphs.ToArray();
            for (int i = 0; i < graphs.Length; i++)
                graphs[i].Close();

            AnimationTreeGraph graph = new AnimationTreeGraph();
            graph.PopulateGraph((AnimationTree)animTrees.SelectedItems[0].Tag);
            graph.FormClosed += Graph_FormClosed;
            _graphs.Add(graph);

            graph.Show(AnimTreeEditor.DockPanel, DockState.Document);
        }

        private void Graph_FormClosed(object sender, FormClosedEventArgs e)
        {
            AnimationTreeGraph graph = (AnimationTreeGraph)sender;
            graph.FormClosed -= Graph_FormClosed;
            _graphs.Remove(graph);
        }
    }
}

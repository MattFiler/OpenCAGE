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

        public AnimationSets()
        {
            InitializeComponent();

            List<PAK2.File> files = Singleton.Global.Animations.Entries.FindAll(o => o.Filename.ToUpper().Contains("ANIM_TREE_DB"));
            List<AnimTreeDB> dbs = new List<AnimTreeDB>();
            foreach (PAK2.File file in files)
            {
                dbs.Add(new AnimTreeDB(file.Content, Singleton.AnimationStrings_Debug, file.Filename));
#if DEBUG
                File.WriteAllText(Path.GetFileName(file.Filename) + ".json", JsonConvert.SerializeObject(dbs[dbs.Count - 1], Newtonsoft.Json.Formatting.Indented));
#endif
            }

            animSets.BeginUpdate();
            foreach (AnimTreeDB db in dbs.OrderBy(o => o.Set))
                animSets.Items.Add(new ListViewItem(db.Entries[0].Set) { Tag = db });
            animSets.EndUpdate();

#if DEBUG
            //test code: loads PERSISTENT_ACT_GUN_LAYER_FP within HUMANOID
            animSets.Items[3].Selected = true;
#endif
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

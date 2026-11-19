using CATHODE;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Asks which character - and optionally which of its contexts - should be able to reach a
    /// blend set.
    /// </summary>
    public partial class BlendSetUserPicker : Form
    {
        /// <summary>The character database chosen, or null if the dialog was cancelled.</summary>
        public AnimClipDB Database { get; private set; }

        /// <summary>The context chosen, or null for the character itself.</summary>
        public AnimClipDB.Context Context { get; private set; }

        private readonly CathodeLib.Animation _animations;
        private readonly GlobalAnimClipDB.BlendSet _set;

        public BlendSetUserPicker(CathodeLib.Animation animations, GlobalAnimClipDB.BlendSet set)
        {
            InitializeComponent();

            _animations = animations;
            _set = set;

            Text = "Give '" + set + "' to";
            Icon = SharedFormIcon.Icon;

            Build();
        }

        private void _search_TextChanged(object sender, EventArgs e)
        {
            Build();
        }

        private void _tree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            _ok.Enabled = _tree.SelectedNode?.Tag != null;
        }

        private void _tree_DoubleClick(object sender, EventArgs e)
        {
            if (_tree.SelectedNode?.Tag != null) Accept();
        }

        private void _ok_Click(object sender, EventArgs e)
        {
            Accept();
        }

        /* Characters that already carry blend sets first: those are the ones a blend set is likely
         * to belong to, and the list is 400 long otherwise. */
        private void Build()
        {
            string search = _search.Text.Trim();
            _tree.BeginUpdate();
            _tree.Nodes.Clear();

            TreeNode carrying = _tree.Nodes.Add("Characters that already use blend sets");
            TreeNode rest = _tree.Nodes.Add("Everything else");

            foreach (AnimClipDB database in _animations.ClipDatabases.OrderBy(x => x.Character, StringComparer.OrdinalIgnoreCase))
            {
                if (search.Length != 0 && (database.Character ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;

                bool uses = database.BlendSets.Count != 0 || database.Contexts.Any(x => x.BlendSets.Count != 0);
                TreeNode node = (uses ? carrying : rest).Nodes.Add(database.Character);
                node.Tag = new Choice { Database = database, Context = null };

                foreach (AnimClipDB.Context context in database.Contexts.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                    node.Nodes.Add(new TreeNode(context.Name.Length == 0 ? "(default)" : context.Name)
                    { Tag = new Choice { Database = database, Context = context } });
            }

            if (carrying.Nodes.Count != 0 && carrying.Nodes.Count < 40) carrying.Expand();
            if (carrying.Nodes.Count == 0) carrying.Remove();
            if (rest.Nodes.Count == 0) rest.Remove();

            _tree.EndUpdate();
            _ok.Enabled = false;
        }

        private void Accept()
        {
            Choice choice = _tree.SelectedNode?.Tag as Choice;
            if (choice == null) return;

            Database = choice.Database;
            Context = choice.Context;
            DialogResult = DialogResult.OK;
            Close();
        }

        private class Choice
        {
            public AnimClipDB Database;
            public AnimClipDB.Context Context;
        }
    }
}

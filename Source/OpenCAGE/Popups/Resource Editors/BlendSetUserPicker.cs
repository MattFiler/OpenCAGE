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
    /// blend set. Built in code rather than the designer because it is one list and two buttons.
    /// </summary>
    public class BlendSetUserPicker : Form
    {
        /// <summary>The character database chosen, or null if the dialog was cancelled.</summary>
        public AnimClipDB Database { get; private set; }

        /// <summary>The context chosen, or null for the character itself.</summary>
        public AnimClipDB.Context Context { get; private set; }

        private readonly TreeView _tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
        private readonly TextBox _search = new TextBox { Dock = DockStyle.Fill };
        private readonly Button _ok = new Button { Text = "Add", Width = 90, Enabled = false };
        private readonly Button _cancel = new Button { Text = "Cancel", Width = 90, DialogResult = DialogResult.Cancel };
        private readonly CathodeLib.Animation _animations;
        private readonly GlobalAnimClipDB.BlendSet _set;

        public BlendSetUserPicker(CathodeLib.Animation animations, GlobalAnimClipDB.BlendSet set)
        {
            _animations = animations;
            _set = set;

            Text = "Give '" + set + "' to";
            ClientSize = new Size(460, 480);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = MaximizeBox = false;
            Icon = SharedFormIcon.Icon;
            AcceptButton = _ok;
            CancelButton = _cancel;

            Panel top = new Panel { Dock = DockStyle.Top, Height = 30, Padding = new Padding(8, 5, 8, 0) };
            top.Controls.Add(_search);
            top.Controls.Add(new Label { Text = "Search", Dock = DockStyle.Left, Width = 46, TextAlign = ContentAlignment.MiddleLeft });

            Panel bottom = new Panel { Dock = DockStyle.Bottom, Height = 42 };
            _ok.Location = new Point(ClientSize.Width - 194, 9);
            _cancel.Location = new Point(ClientSize.Width - 98, 9);
            _ok.Anchor = _cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _ok.Click += (s, e) => { Accept(); };
            bottom.Controls.Add(_ok);
            bottom.Controls.Add(_cancel);

            Panel body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 4) };
            body.Controls.Add(_tree);

            Controls.Add(body);
            Controls.Add(top);
            Controls.Add(bottom);

            _search.TextChanged += (s, e) => Build();
            _tree.AfterSelect += (s, e) => _ok.Enabled = _tree.SelectedNode?.Tag != null;
            _tree.DoubleClick += (s, e) => { if (_tree.SelectedNode?.Tag != null) Accept(); };

            Build();
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

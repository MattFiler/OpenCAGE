using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.MCP;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.Prefabs
{
    /// <summary>
    /// Create Entity > Insert Prefab...: the prefab library (Prefabs folder beside OpenCAGE) by category. Insert imports the prefab's
    /// package if the level lacks it (not undoable, as any composite import) and places an instance in the composite (one undo step).
    /// </summary>
    internal partial class InsertPrefabWindow : BaseWindow
    {
        private static InsertPrefabWindow _open;
        private static string _lastPrefab = "EnemySpawner";

        private readonly Composite _into;
        private readonly string _page;
        private readonly Point? _nodeAt;

        private readonly List<string> _problems = new List<string>();
        private string _suggestedName;

        private sealed class CharacterChoice
        {
            public Composite Archetype;
            public string Text;
            public override string ToString() => Text;
        }

        public InsertPrefabWindow(Composite into, string page = null, Point? nodeAt = null) : base(WindowClosesOn.NEW_COMPOSITE_SELECTION | WindowClosesOn.COMMANDS_RELOAD)
        {
            _into = into;
            _page = page;
            _nodeAt = nodeAt;
            InitializeComponent();
            Icon = SharedFormIcon.Icon;
            StayAboveEditor = true;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/prefabs/");

            Theming.ThemeManager.ApplyCurrent(this);
            Fill();
        }

        private void _tree_AfterSelect(object sender, TreeViewEventArgs e) => ShowSelected();
        private void _insert_Click(object sender, EventArgs e) => Insert();
        private void _folder_Click(object sender, EventArgs e) => OpenFolder();

        private void _tree_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node?.Tag is Prefab) Insert();
        }

        private void _optionsPanel_Resize(object sender, EventArgs e)
        {
            _status.Width = _optionsPanel.ClientSize.Width;
            _insert.Location = new Point(_optionsPanel.ClientSize.Width - _insert.Width, _optionsPanel.ClientSize.Height - _insert.Height - 2);
            _folder.Location = new Point(0, _optionsPanel.ClientSize.Height - _folder.Height - 2);
        }

        #region Opening it
        /// <summary>The window for <paramref name="into"/> (one at a time): the node goes on <paramref name="page"/> at <paramref name="nodeAt"/> when given.</summary>
        public static InsertPrefabWindow Open(Composite into, string page = null, Point? nodeAt = null)
        {
            if (into == null || Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands == null)
                return null;
            _open?.Close();
            _open = new InsertPrefabWindow(into, page, nodeAt);
            _open.FormClosed += (s, e) => { if (_open == s) _open = null; };
            if (Singleton.Editor != null) _open.Show(Singleton.Editor); else _open.Show();
            return _open;
        }

        /// <summary>
        /// The "Insert Prefab..." item for a Create Entity menu: <paramref name="composite"/> says where it goes when clicked, and
        /// <paramref name="where"/> (optional) the flowgraph page and canvas point for its node.
        /// </summary>
        public static ToolStripMenuItem MenuItem(Func<Composite> composite, Func<(string Page, PointF At)> where = null)
        {
            ToolStripMenuItem item = new ToolStripMenuItem("Insert Prefab...")
            {
                ToolTipText = "Place ready-made gameplay script: kill volume, enemy spawner with loot, ally, hunter, kill counter + objective, out-of-view spawn point, safe despawn - or a package of your own from the Prefabs folder.",
            };
            EditorIcons.Bind(item, EditorIcon.CompositeInstance);
            item.Click += (s, e) =>
            {
                Composite into = composite?.Invoke();
                if (into == null) return;
                if (where == null) { Open(into); return; }
                (string page, PointF at) = where();
                Open(into, page, new Point((int)at.X, (int)at.Y));
            };
            return item;
        }
        #endregion

        #region The list
        private void Fill()
        {
            _problems.Clear();
            List<Prefab> prefabs = PrefabLibrary.All(_problems);
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            TreeNode select = null;
            foreach (IGrouping<string, Prefab> group in prefabs.GroupBy(o => o.Category))
            {
                TreeNode category = new TreeNode(group.Key);
                foreach (Prefab prefab in group)
                {
                    TreeNode node = new TreeNode(prefab.Name) { Tag = prefab, ToolTipText = PrefabLibrary.Paragraphs(prefab).FirstOrDefault() };
                    category.Nodes.Add(node);
                    if (select == null && string.Equals(prefab.Key, _lastPrefab, StringComparison.OrdinalIgnoreCase))
                        select = node;
                }
                _tree.Nodes.Add(category);
            }
            _tree.ExpandAll();
            _tree.EndUpdate();
            if (select == null && _tree.Nodes.Count != 0 && _tree.Nodes[0].Nodes.Count != 0)
                select = _tree.Nodes[0].Nodes[0];
            _tree.SelectedNode = select;
            if (select == null)
                ShowSelected();
        }

        private Prefab Selected => _tree.SelectedNode?.Tag as Prefab;

        private void ShowSelected()
        {
            Prefab prefab = Selected;
            Commands commands = Content?.Level?.Commands;
            _insert.Enabled = prefab != null && commands != null;
            if (prefab == null)
            {
                _title.Text = _tree.Nodes.Count == 0 ? "No prefabs" : "";
                _about.Text = _tree.Nodes.Count == 0
                    ? "There are no prefab packages in " + PrefabLibrary.Folder + "." + (_problems.Count != 0 ? "\r\n\r\n" + string.Join("\r\n", _problems) : "")
                    : "";
                _status.Text = "";
                ShowSlots(null, commands);
                return;
            }
            _lastPrefab = prefab.Key;
            _title.Text = prefab.Name;
            List<string> text = PrefabLibrary.Paragraphs(prefab).ToList();
            Composite inLevel = commands == null ? null : PrefabPlacer.InLevel(prefab, commands);
            if (inLevel != null)
            {
                List<(string Name, string Kind)> pins = PrefabPlacer.Pins(commands, inLevel);
                if (pins.Count != 0)
                    text.Add("Its pins here: " + string.Join(", ", pins.Select(o => o.Name + " (" + o.Kind + ")")) + ".");
            }
            if (prefab.Catalog?.Hint != null)
                text.Add("After inserting: " + prefab.Catalog.Hint);
            if (!prefab.BuiltIn)
                text.Add("From " + prefab.File + (prefab.Manifest?.SourceLevel != null ? " (exported from " + prefab.Manifest.SourceLevel + ")" : "") + ".");
            _about.Text = string.Join("\r\n\r\n", text);

            string suggested = commands == null ? "" : SuggestName(commands, inLevel, prefab);
            if (string.IsNullOrWhiteSpace(_name.Text) || _name.Text == _suggestedName)
                _name.Text = suggested;
            _suggestedName = suggested;

            _status.Text = (inLevel != null
                ? "This level has the prefab (" + inLevel.name + "): Insert places an instance of it."
                : "Insert imports " + prefab.RootName + " from " + Path.GetFileName(prefab.File) + " first (not undoable; saved with the level), then places it.")
                + (commands != null && PrefabPlacer.MissingCharacter(prefab, commands) is string missing ? " Its NPC (" + missing + ") is brought in from " + (PrefabPlacer.CharacterSource(prefab) ?? "a level that has it") + " first." : "")
                + " It goes at the origin of " + EditorUtils.GetCompositeName(_into) + ": move it with the Viewport's gizmo or its position.";
            ShowSlots(prefab, commands);
        }

        //The instance's name as the editor would number it
        private string SuggestName(Commands commands, Composite inLevel, Prefab prefab)
        {
            if (inLevel != null)
                return DockPanels.CompositeDisplay.NewInstanceName(commands, _into, inLevel);
            return McpNames.Leaf(prefab.RootName ?? prefab.Key) + "_1";
        }

        private void ShowSlots(Prefab prefab, Commands commands)
        {
            bool character = prefab?.Catalog?.CharacterEntity != null && commands != null;
            bool box = prefab?.Catalog?.BoxEntity != null;
            _characterLabel.Visible = _character.Visible = character;
            _boxLabel.Visible = _boxX.Visible = _boxY.Visible = _boxZ.Visible = box && !character;
            if (character)
            {
                _character.Items.Clear();
                Composite packaged = PrefabPlacer.PackagedCharacter(prefab, commands);
                string packagedName = packaged?.name ?? prefab.Catalog.CharacterArchetype;
                _character.Items.Add(new CharacterChoice() { Archetype = null, Text = "As packaged" + (packagedName != null ? " (" + packagedName + ")" : "") });
                foreach (Composite archetype in PrefabPlacer.NpcArchetypes(commands))
                    if (packaged == null || archetype != packaged)
                        _character.Items.Add(new CharacterChoice() { Archetype = archetype, Text = archetype.name });
                _character.SelectedIndex = 0;
            }
            if (box)
            {
                //What the packaged box is: 4 x 2 x 4 m for the Kill Volume
                _boxX.Value = 2m; _boxY.Value = 1m; _boxZ.Value = 2m;
            }
        }
        #endregion

        #region Insert
        private void Insert()
        {
            Prefab prefab = Selected;
            LevelContent content = Content;
            if (prefab == null || content?.Level?.Commands == null)
                return;
            if (!content.Level.Commands.Entries.Contains(_into))
            {
                MessageBox.Show("The composite this window was opened for is no longer in the level.", "Insert Prefab", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
                return;
            }
            string name = _name.Text.Trim();
            if (name.Length != 0)
            {
                string problem = EditorUtils.UnstorableTextProblem(name, "This name");
                if (problem != null) { MessageBox.Show(problem, "Can't use this name", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            }
            if (name == _suggestedName)
                name = null; //numbered afresh when it is made, in case another arrived meanwhile

            PrefabPlacement placement = new PrefabPlacement()
            {
                Prefab = prefab,
                Into = _into,
                Name = name,
                Character = _character.Visible ? (_character.SelectedItem as CharacterChoice)?.Archetype : null,
                BoxHalfDimensions = _boxX.Visible && !IsPackagedBox() ? new System.Numerics.Vector3((float)_boxX.Value, (float)_boxY.Value, (float)_boxZ.Value) : (System.Numerics.Vector3?)null,
                Page = _page,
                NodeAt = _nodeAt,
                LabelPrefix = "",
                Show = true,
            };

            //A character prefab's NPC is the game's own archetype, which the package leaves out: brought in from a level that has it
            string missing = PrefabPlacer.MissingCharacter(prefab, content.Level.Commands);
            if (missing != null)
            {
                string from = PrefabPlacer.CharacterSource(prefab);
                if (from == null)
                {
                    MessageBox.Show(prefab.Name + " places " + missing + ", which this level does not have, and no level of this install that has it was found (" + string.Join(", ", prefab.Catalog.CharacterLevels) + ").",
                        "Insert Prefab", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (MessageBox.Show(prefab.Name + "'s NPC is " + missing + ", which this level does not have yet.\n\nBring it in from " + from + " now? That loads " + from + " (it takes a little while) and, like any import, cannot be undone - the level keeps it when you save.",
                    "Insert Prefab", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }

            PrefabPlaced placed;
            Cursor = Cursors.WaitCursor;
            try
            {
                placed = PrefabPlacer.Place(content, placement);
            }
            catch (McpError e)
            {
                Cursor = Cursors.Default;
                MessageBox.Show(e.Message, "Insert Prefab", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ShowSelected();
                return;
            }
            catch (Exception e)
            {
                Cursor = Cursors.Default;
                Debug.Log("Prefabs", "Insert failed: " + e);
                MessageBox.Show("The prefab could not be inserted:\n\n" + e.Message, "Insert Prefab", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ShowSelected();
                return;
            }
            Cursor = Cursors.Default;
            //Something the user should know beyond "it is there": a copy made for another character, links that copy could not keep
            List<string> worth = placed.Notes.Where(o => o.StartsWith("Made ") || o.Contains("left out of the copy")).ToList();
            if (worth.Count != 0)
                MessageBox.Show(string.Join("\n\n", worth), "Insert Prefab", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LastPlaced = placed;
            Close();
        }

        private bool IsPackagedBox() => _boxX.Value == 2m && _boxY.Value == 1m && _boxZ.Value == 2m;

        /// <summary>What the last Insert placed (for anyone driving the window).</summary>
        public static PrefabPlaced LastPlaced { get; private set; }

        private static void OpenFolder()
        {
            try
            {
                Directory.CreateDirectory(PrefabLibrary.Folder);
                Process.Start(new ProcessStartInfo() { FileName = PrefabLibrary.Folder, UseShellExecute = true });
            }
            catch (Exception e)
            {
                MessageBox.Show("The prefab folder could not be opened: " + e.Message, "Insert Prefab", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        #endregion
    }
}

using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Pick composites from any number of levels, browsing each level's composite table without loading
    /// it. Two uses: opened from the editor it imports the picks into the loaded level and refreshes the
    /// browser; opened as a picker (<see cref="PickOnly"/>) it only hands the selection back, for the
    /// Create Level dialog to apply once the level exists.
    /// </summary>
    public partial class ImportComposites : BaseWindow
    {
        public bool PickOnly { get; }
        public CompositeSelection Selection { get; }

        private readonly string _excludedLevel;
        private readonly Dictionary<ShortGuid, string> _presentInDestination;
        private List<CompositeIndexEntry> _shown = new List<CompositeIndexEntry>();
        private string _shownLevel;
        private bool _populating;

        /// <param name="pickOnly">Return the selection instead of importing it into the loaded level.</param>
        /// <param name="excludeLevel">A level to leave out of the list (the one being imported into).</param>
        /// <param name="selection">A selection to start from, so a picker can be reopened to adjust it.</param>
        public ImportComposites(bool pickOnly, string excludeLevel = null, CompositeSelection selection = null) : base(pickOnly ? WindowClosesOn.NONE : WindowClosesOn.COMMANDS_RELOAD)
        {
            PickOnly = pickOnly;
            Selection = selection ?? new CompositeSelection();
            _excludedLevel = excludeLevel;

            InitializeComponent();

            if (PickOnly)
            {
                Text = "Choose Composites To Import";
                importButton.Text = "OK";
                //A level that does not exist yet has nothing to overwrite
                overwriteComposites.Visible = false;
            }
            else
            {
                Text = "Import Composites Into " + (Content?.Level?.Name ?? "Level");
                _presentInDestination = new Dictionary<ShortGuid, string>();
                if (Content?.Level?.Commands != null)
                    foreach (Composite composite in Content.Level.Commands.Entries)
                        if (composite != null && !_presentInDestination.ContainsKey(composite.shortGUID))
                            _presentInDestination[composite.shortGUID] = composite.name;
            }

            includeChildren.Checked = Selection.IncludeChildren;
            overwriteComposites.Checked = Selection.OverwriteComposites;
            overwriteAssets.Checked = Selection.OverwriteAssets;

            levelList.BeginUpdate();
            foreach (string level in EditorUtils.GetEditableLevels())
            {
                if (_excludedLevel != null && string.Equals(level, _excludedLevel, StringComparison.OrdinalIgnoreCase))
                    continue;
                levelList.Items.Add(level);
            }
            levelList.EndUpdate();

            string first = Selection.Levels.FirstOrDefault()?.Level;
            if (first != null && levelList.Items.Contains(first))
                levelList.SelectedItem = first;
            else if (levelList.Items.Count > 0)
                levelList.SelectedIndex = 0;

            UpdateSummary();
        }

        private void levelList_SelectedIndexChanged(object sender, EventArgs e)
        {
            PopulateComposites();
        }

        private void filterBox_TextChanged(object sender, EventArgs e)
        {
            PopulateComposites();
        }

        private void PopulateComposites()
        {
            _shownLevel = levelList.SelectedItem?.ToString();
            _shown = new List<CompositeIndexEntry>();
            _populating = true;
            compositeList.BeginUpdate();
            compositeList.Items.Clear();

            if (_shownLevel != null)
            {
                List<CompositeIndexEntry> composites;
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    composites = CompositeIndexCache.Get(_shownLevel);
                }
                catch (Exception ex)
                {
                    composites = new List<CompositeIndexEntry>();
                    Debug.Log("Import", "Could not list composites in " + _shownLevel + ": " + ex.Message);
                }
                Cursor.Current = Cursors.Default;

                CompositeSelection.LevelPick pick = Selection.Levels.FirstOrDefault(o => string.Equals(o.Level, _shownLevel, StringComparison.OrdinalIgnoreCase));
                string filter = filterBox.Text.Trim();
                foreach (CompositeIndexEntry composite in composites)
                {
                    if (filter.Length != 0 && composite.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    _shown.Add(composite);
                    string label = composite.Name;
                    if (_presentInDestination != null && _presentInDestination.ContainsKey(composite.ID))
                        label += "   (already in this level)";
                    compositeList.Items.Add(label, pick != null && pick.Composites.ContainsKey(composite.ID));
                }
            }

            compositeList.EndUpdate();
            _populating = false;
        }

        private void compositeList_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (_populating || _shownLevel == null || e.Index < 0 || e.Index >= _shown.Count)
                return;

            CompositeIndexEntry composite = _shown[e.Index];
            CompositeSelection.LevelPick pick = Selection.GetOrAdd(_shownLevel);
            if (e.NewValue == CheckState.Checked)
                pick.Composites[composite.ID] = composite.Name;
            else
                pick.Composites.Remove(composite.ID);

            //ItemCheck fires before the box repaints, so count what it is about to become
            UpdateSummary();
        }

        private void checkShown_Click(object sender, EventArgs e)
        {
            SetShown(true);
        }

        private void uncheckShown_Click(object sender, EventArgs e)
        {
            SetShown(false);
        }

        private void SetShown(bool check)
        {
            if (_shownLevel == null) return;
            CompositeSelection.LevelPick pick = Selection.GetOrAdd(_shownLevel);
            _populating = true;
            compositeList.BeginUpdate();
            for (int i = 0; i < _shown.Count; i++)
            {
                if (check) pick.Composites[_shown[i].ID] = _shown[i].Name;
                else pick.Composites.Remove(_shown[i].ID);
                compositeList.SetItemChecked(i, check);
            }
            compositeList.EndUpdate();
            _populating = false;
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            summaryLabel.Text = Selection.Summary();
        }

        private void importButton_Click(object sender, EventArgs e)
        {
            Selection.IncludeChildren = includeChildren.Checked;
            Selection.OverwriteComposites = overwriteComposites.Checked;
            Selection.OverwriteAssets = overwriteAssets.Checked;
            Selection.Prune();

            if (PickOnly)
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            if (Selection.IsEmpty)
            {
                MessageBox.Show("Tick the composites to import first.", "Nothing selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (Content?.Level?.Commands == null)
            {
                MessageBox.Show("No level is loaded to import into.", "No level", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Enabled = false;
            Cursor.Current = Cursors.WaitCursor;
            CompositeImporter.Result result;
            try
            {
                result = ImportIntoLoadedLevel();
            }
            catch (Exception ex)
            {
                Cursor.Current = Cursors.Default;
                Enabled = true;
                MessageBox.Show("The import did not complete:\n\n" + ex.Message + "\n\nThe level in the editor may hold a partial import - reload it without saving if in doubt.", "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Cursor.Current = Cursors.Default;

            MessageBox.Show("Imported " + result.Ported.Count + " composite" + (result.Ported.Count == 1 ? "" : "s") + " (" + result.Renderables + " renderables, " + result.CollisionMappings + " collision mappings, " + result.PhysicsSystems + " physics systems).\n\nSave the level to keep them.", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }

        /* Port straight into the level open in the editor: nothing touches disk until the user saves */
        private CompositeImporter.Result ImportIntoLoadedLevel()
        {
            Level destination = Content.Level;

            //A composite about to be replaced may be open in a tab holding the old object
            if (Selection.OverwriteComposites && Selection.Levels.Any(l => l.Composites.Keys.Any(id => destination.Commands.GetComposite(id) != null)))
                Singleton.Editor.CompositeBrowser.CloseAllChildTabs();

            Singleton.OnCompositeAddPending?.Invoke();

            List<Composite> ported = new List<Composite>();
            CompositeImporter.Result result = CompositeImporter.Import(Selection, destination, (composite, layouts) =>
            {
                ported.Add(composite);
                //Registers the composite the way a newly created one is (dirty flag, compatibility entry, viewer), then its own pages replace the default page that gives it
                Singleton.OnCompositeAdded?.Invoke(composite);
                FlowgraphLayoutManager.ImportLayouts(composite, layouts);
            });

            if (ported.Count > 0)
                Singleton.Editor.CompositeBrowser.SelectCompositeAndReloadList(ported[0]);
            return result;
        }
    }
}

using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using OpenCAGE.Popups.Base;
using OpenCAGE.Popups.UserControls;
using OpenCAGE.UnityConnection;
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
        private readonly CompositeTree _tree;
        private string _shownLevel;
        private Dictionary<ShortGuid, CompositeIndexEntry> _shownIndex = new Dictionary<ShortGuid, CompositeIndexEntry>();

        /// <param name="pickOnly">Return the selection instead of importing it into the loaded level.</param>
        /// <param name="excludeLevel">A level to leave out of the list (the one being imported into).</param>
        /// <param name="selection">A selection to start from, so a picker can be reopened to adjust it.</param>
        public ImportComposites(bool pickOnly, string excludeLevel = null, CompositeSelection selection = null) : base(pickOnly ? WindowClosesOn.NONE : WindowClosesOn.COMMANDS_RELOAD)
        {
            PickOnly = pickOnly;
            Selection = selection ?? new CompositeSelection();
            _excludedLevel = excludeLevel;

            InitializeComponent();
            _tree = new CompositeTree(compositeTree);
            _tree.SelectionChanged += OnTreeSelectionChanged;

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

            overwriteComposites.Checked = Selection.OverwriteComposites;
            overwriteAssets.Checked = Selection.OverwriteAssets;
            openAfterImport.Checked = SettingsManager.GetBool(Settings.CompositeImportOpenAfter, false);

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
            _tree.Filter = filterBox.Text;
        }

        /* The chosen level's composites, from its COMMANDS table alone, with what each instances so the
           tree can follow nesting; what was ticked for this level before comes back ticked */
        private void PopulateComposites()
        {
            _shownLevel = levelList.SelectedItem?.ToString();
            _shownIndex = new Dictionary<ShortGuid, CompositeIndexEntry>();

            List<CompositeTree.Item> items = new List<CompositeTree.Item>();
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

                foreach (CompositeIndexEntry composite in composites)
                {
                    if (_shownIndex.ContainsKey(composite.ID))
                        continue;
                    _shownIndex[composite.ID] = composite;
                    items.Add(new CompositeTree.Item()
                    {
                        Id = composite.ID,
                        Name = composite.Name,
                        Kind = CompositeTree.KindOf(composite.ID, composite.Name, composite.IsRoot),
                        Note = _presentInDestination != null && _presentInDestination.ContainsKey(composite.ID) ? "(already in this level)" : null,
                    });
                }
            }

            CompositeSelection.LevelPick pick = _shownLevel == null ? null : Selection.Levels.FirstOrDefault(o => string.Equals(o.Level, _shownLevel, StringComparison.OrdinalIgnoreCase));
            _tree.Filter = filterBox.Text;
            _tree.Load(items, InstancesOf, pick?.Composites.Keys);
        }

        private IEnumerable<ShortGuid> InstancesOf(ShortGuid id)
        {
            return _shownIndex.TryGetValue(id, out CompositeIndexEntry entry) && entry.Instances != null
                ? entry.Instances
                : Enumerable.Empty<ShortGuid>();
        }

        /* The tree is the truth for the level it shows; the selection keeps a copy per level */
        private void OnTreeSelectionChanged()
        {
            if (_shownLevel != null)
            {
                CompositeSelection.LevelPick pick = Selection.GetOrAdd(_shownLevel);
                pick.Composites.Clear();
                foreach (ShortGuid id in _tree.Ticked)
                    pick.Composites[id] = _tree.NameOf(id) ?? "";
                pick.Implied.Clear();
                foreach (ShortGuid id in _tree.Implied)
                    pick.Implied[id] = _tree.NameOf(id) ?? "";
            }
            UpdateSummary();
        }

        private void checkShown_Click(object sender, EventArgs e)
        {
            _tree.SetShown(true);
        }

        private void uncheckShown_Click(object sender, EventArgs e)
        {
            _tree.UntickAll();
        }

        private void UpdateSummary()
        {
            summaryLabel.Text = Selection.Summary();
        }

        private void importButton_Click(object sender, EventArgs e)
        {
            Selection.OverwriteComposites = overwriteComposites.Checked;
            Selection.OverwriteAssets = overwriteAssets.Checked;
            SettingsManager.SetBool(Settings.CompositeImportOpenAfter, openAfterImport.Checked);
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
            List<Composite> ported = new List<Composite>();
            //The viewer follows the import in its script copy only; the rebuild at the end shows it
            Send.BeginSceneBatch();
            try
            {
                result = ImportIntoLoadedLevel(ported);
            }
            catch (Exception ex)
            {
                Send.EndSceneBatch();
                Cursor.Current = Cursors.Default;
                Enabled = true;
                MessageBox.Show("The import did not complete:\n\n" + ex.Message + "\n\nThe level in the editor may hold a partial import - reload it without saving if in doubt.", "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Cursor.Current = Cursors.Default;

            //The level holds the import whatever happens from here: showing it is a separate matter
            string opening = null;
            try
            {
                if (ported.Count > 0 && openAfterImport.Checked)
                    CompositeImporter.OpenPortedComposite(ported[0]);
            }
            catch (Exception ex)
            {
                opening = ex.Message;
            }
            finally
            {
                Send.EndSceneBatch();
            }

            string deadProxies = result.DeadProxies.Describe("this level");
            MessageBox.Show("Imported " + result.Ported.Count + " composite" + (result.Ported.Count == 1 ? "" : "s") + " (" + result.Renderables + " renderables, " + result.CollisionMappings + " collision mappings, " + result.PhysicsSystems + " physics systems).\n\nSave the level to keep them."
                + (opening == null ? "" : "\n\nThe composite could not be opened in the editor afterwards: " + opening)
                + (deadProxies == "" ? "" : "\n\n" + deadProxies),
                "Complete", MessageBoxButtons.OK, deadProxies == "" ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            Close();
        }

        /* Port straight into the level open in the editor: nothing touches disk until the user saves */
        private CompositeImporter.Result ImportIntoLoadedLevel(List<Composite> ported)
        {
            Level destination = Content.Level;

            //A composite about to be replaced may be open in a tab holding the old object
            if (Selection.OverwriteComposites && Selection.Levels.Any(l => l.Composites.Keys.Concat(l.Implied.Keys).Any(id => destination.Commands.GetComposite(id) != null)))
                Singleton.Editor.CompositeBrowser.CloseAllChildTabs();

            Singleton.OnCompositeAddPending?.Invoke();

            /* With overwrite on, the porter also replaces every nested composite the level already holds.
               Anything mirroring the level (the viewer above all) must hear that the old one went before
               the new one arrives, or it ends up holding two objects for one ID. */
            Dictionary<ShortGuid, Composite> existing = destination.Commands.Entries.Where(o => o != null).GroupBy(o => o.shortGUID).ToDictionary(o => o.Key, o => o.First());

            CompositeImporter.Result result = CompositeImporter.Import(Selection, destination, (composite, layouts) =>
            {
                ported.Add(composite);
                if (existing.TryGetValue(composite.shortGUID, out Composite replaced) && !ReferenceEquals(replaced, composite))
                    Singleton.OnCompositeDeleted?.Invoke(replaced);
                //Registers the composite the way a newly created one is (dirty flag, compatibility entry, viewer), then its own pages replace the default page that gives it
                Singleton.OnCompositeAdded?.Invoke(composite);
                FlowgraphLayoutManager.ImportLayouts(composite, layouts);
            });
            return result;
        }
    }
}

using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Port any number of the loaded level's composites to another level (or every level) in one go:
    /// the destination is loaded, receives every ticked composite, and is saved once.
    /// </summary>
    public partial class ExportComposite : BaseWindow
    {
        private CompositeFlowgraphTable _fgLayouts;

        private readonly HashSet<ShortGuid> _selected = new HashSet<ShortGuid>();
        private List<Composite> _shown = new List<Composite>();
        private bool _populating;

        /// <param name="composite">A composite to start with ticked, or null for none.</param>
        public ExportComposite(Composite composite) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();

            levelList.BeginUpdate();
            levelList.Items.AddRange(EditorUtils.GetEditableLevels().ToArray());
            levelList.Items.Remove(Content.Level.Name);
            levelList.EndUpdate();

            if (levelList.Items.Count > 0)
                levelList.SelectedIndex = 0;

            if (composite != null)
                _selected.Add(composite.shortGUID);

            PopulateComposites();
        }

        private void portToAllLevels_CheckedChanged(object sender, EventArgs e)
        {
            levelList.Enabled = !portToAllLevels.Checked;
            label1.Enabled = !portToAllLevels.Checked;
        }

        private void filterBox_TextChanged(object sender, EventArgs e)
        {
            PopulateComposites();
        }

        private void PopulateComposites()
        {
            string filter = filterBox.Text.Trim();
            _shown = Content.Level.Commands.Entries
                .Where(o => o != null && (filter.Length == 0 || o.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(o => o.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _populating = true;
            compositeList.BeginUpdate();
            compositeList.Items.Clear();
            foreach (Composite composite in _shown)
                compositeList.Items.Add(composite.name, _selected.Contains(composite.shortGUID));
            compositeList.EndUpdate();
            _populating = false;

            UpdateSummary();
        }

        private void compositeList_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (_populating || e.Index < 0 || e.Index >= _shown.Count)
                return;
            if (e.NewValue == CheckState.Checked)
                _selected.Add(_shown[e.Index].shortGUID);
            else
                _selected.Remove(_shown[e.Index].shortGUID);
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
            _populating = true;
            compositeList.BeginUpdate();
            for (int i = 0; i < _shown.Count; i++)
            {
                if (check) _selected.Add(_shown[i].shortGUID);
                else _selected.Remove(_shown[i].shortGUID);
                compositeList.SetItemChecked(i, check);
            }
            compositeList.EndUpdate();
            _populating = false;
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            summaryLabel.Text = _selected.Count == 0 ? "Nothing selected" : _selected.Count + " composite" + (_selected.Count == 1 ? "" : "s") + " selected";
        }

        private void export_Click(object sender, System.EventArgs e)
        {
            List<Composite> composites = _selected.Select(id => Content.Level.Commands.GetComposite(id)).Where(o => o != null).ToList();
            if (composites.Count == 0)
            {
                MessageBox.Show("Tick the composites to port first.", "Nothing selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<string> targetLevels;
            if (portToAllLevels.Checked)
            {
                string currentLevel = Content.Level.Name;
                targetLevels = EditorUtils.GetEditableLevels()
                    .Where(levelName => !string.Equals(levelName, currentLevel, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            else
            {
                if (levelList.SelectedItem == null)
                {
                    MessageBox.Show("Please select a destination level.", "No level selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                targetLevels = new List<string> { levelList.SelectedItem.ToString() };
            }

            if (targetLevels.Count == 0)
            {
                MessageBox.Show("There are no destination levels to port to.", "Nothing to do", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Enabled = false;
            Cursor.Current = Cursors.WaitCursor;
            int ported = 0;
            try
            {
                foreach (string levelName in targetLevels)
                    ported += PortCompositesToLevel(composites, levelName);
            }
            catch (Exception ex)
            {
                Cursor.Current = Cursors.Default;
                Enabled = true;
                MessageBox.Show("The port did not complete:\n\n" + ex.Message, "Port failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Cursor.Current = Cursors.Default;

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            string destinationLabel = portToAllLevels.Checked
                ? (targetLevels.Count + " levels")
                : ("'" + targetLevels[0] + "'");
            MessageBox.Show("Finished porting " + composites.Count + " composite" + (composites.Count == 1 ? "" : "s") + " (" + ported + " including the composites they instance) to " + destinationLabel + "!", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

            this.Close();
        }

        private int PortCompositesToLevel(List<Composite> composites, string levelName)
        {
            Level lvl = new Level(Singleton.PathToAI + "/DATA/ENV/" + levelName, Singleton.Global, false);
            {
                ProgressUI loadProgress = new ProgressUI();
                loadProgress.ShowLevelLoading(lvl);
                loadProgress.BringToFront();
                lvl.Load();
                loadProgress.Close();
                loadProgress.Dispose();
            }

            _fgLayouts = (CompositeFlowgraphTable)CustomTable.ReadTable(lvl.Commands.Filepath, CustomTableType.COMPOSITE_FLOWGRAPHS);
            if (_fgLayouts == null) _fgLayouts = new CompositeFlowgraphTable();

            int ported;
            {
                ProgressUI exportProgress = new ProgressUI();
                exportProgress.ShowTransferring("Porting to " + levelName + "...");
                exportProgress.BringToFront();

                //The copy itself, and the level data it drags along, is CathodeLib's job; this window only
                //adds what CathodeLib cannot know about - the flowgraph pages for each composite it copies.
                CompositePorter porter = new CompositePorter(Content.Level, lvl)
                {
                    OverwriteComposites = overwrite.Checked,
                    OverwriteAssets = overwriteAssets.Checked,
                    Recurse = recurse.Checked,
                };
                porter.OnProgress = exportProgress.DoRefresh;
                porter.OnCompositePorted = (source, copy) =>
                {
                    //Bring over flowgraph layouts (deep-copied; includes predefined fallback)
                    List<CompositeFlowgraphTable.FlowgraphMeta> layouts = FlowgraphLayoutManager.GetLayoutsForPort(source);
                    _fgLayouts.flowgraphs.RemoveAll(o => o.CompositeGUID == source.shortGUID);
                    _fgLayouts.flowgraphs.AddRange(layouts);
                };
                foreach (Composite composite in composites)
                    porter.Port(composite);
                ported = porter.PortedComposites.Count;

                exportProgress.Close();
                exportProgress.Dispose();
            }

            //Close alien down if it's open, it conflicts with our write locks!
            EditorUtils.CloseAI();

            {
                ProgressUI saveProgress = new ProgressUI();
                if (buildAfterPort.Checked)
                {
                    saveProgress.ShowLevelSaving(lvl, true);
                    saveProgress.BringToFront();
                    lvl.SaveInstanced();
                }
                else
                {
                    saveProgress.ShowLevelSaving(lvl, false);
                    saveProgress.BringToFront();
                    lvl.Save();
                }
                saveProgress.Close();
                saveProgress.Dispose();
            }
            CustomTable.WriteTable(lvl.Commands.Filepath, CustomTableType.COMPOSITE_FLOWGRAPHS, _fgLayouts);
            return ported;
        }
    }
}

using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.Popups.Base;
using OpenCAGE.Popups.UserControls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Port any number of the loaded level's composites to any number of other levels in one go:
    /// each destination is loaded, receives every ticked composite, and is saved once.
    /// </summary>
    public partial class ExportComposite : BaseWindow
    {
        private readonly CompositeTree _tree;
        private readonly LevelPicker _levels;

        /// <param name="composite">A composite to start with ticked, or null for none.</param>
        public ExportComposite(Composite composite) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();

            //Every level but the one that is open: that is where the composites already are
            _levels = new LevelPicker(levelList, allLevelsButton, noLevelsButton);
            _levels.Load(EditorUtils.GetEditableLevels().Where(o => !string.Equals(o, Content.Level.Name, StringComparison.OrdinalIgnoreCase)));

            _tree = new CompositeTree(compositeTree);
            _tree.SelectionChanged += UpdateSummary;
            _tree.Load(
                Content.Level.Commands.Entries.Where(o => o != null).Select(o => new CompositeTree.Item() { Id = o.shortGUID, Name = o.name, Kind = CompositeTree.KindOf(Content.EditorUtils.GetCompositeType(o)) }),
                CompositeNesting.InstancesOf(Content.Level.Commands),
                composite == null ? null : new[] { composite.shortGUID });
        }

        private void filterBox_TextChanged(object sender, EventArgs e)
        {
            _tree.Filter = filterBox.Text;
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
            summaryLabel.Text = _tree.Summary();
        }

        private void export_Click(object sender, System.EventArgs e)
        {
            List<Composite> composites = _tree.Ticked.Select(id => Content.Level.Commands.GetComposite(id)).Where(o => o != null).ToList();
            if (composites.Count == 0)
            {
                MessageBox.Show("Tick the composites to port first.", "Nothing selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_levels.Count == 0)
            {
                MessageBox.Show("There are no other levels to port into.", "Nothing to do", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            List<string> targetLevels = _levels.Selected;
            if (targetLevels.Count == 0)
            {
                MessageBox.Show("Tick the level (or levels) to port into.", "No level selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            bool build = buildAfterPort.Checked;
            if (targetLevels.Count > 1
                && MessageBox.Show("Port into these " + targetLevels.Count + " levels?\n\n" + string.Join("\n", targetLevels)
                    + "\n\nEach one is loaded, written to and saved in turn, which takes a while" + (build ? " - and with a build after each, a long while" : "") + ".",
                    "Port into several levels", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            /* Loading, porting and saving a level - let alone building several - takes minutes, so the
               loop runs on a worker while this thread pumps messages, the way the editor's own Save and
               Build does: the progress windows paint, and the editor is not reported as hung. Everything
               that could edit the open level underneath the porter is held off - the editor window is
               disabled, the level picker closed, the undo stack blocked. */
            CommandsEditor editor = Singleton.Editor;
            Level source = Content.Level;
            bool overwriteComposites = overwrite.Checked, overwriteAssetsToo = overwriteAssets.Checked;
            List<string> written = new List<string>();
            List<KeyValuePair<string, DeadProxyReport>> deadProxiesPerLevel = new List<KeyValuePair<string, DeadProxyReport>>();
            string failedLevel = null, failure = null;
            int portedPerLevel = 0;
            Enabled = false;
            Cursor.Current = Cursors.WaitCursor;
            editor?.CloseLevelPicker();
            if (editor != null) editor.Enabled = false;
            if (OpenCAGE.Undo.UndoStack.Current != null) OpenCAGE.Undo.UndoStack.Current.Blocked = true;
            try
            {
                Task work = Task.Run(() =>
                {
                    foreach (string levelName in targetLevels)
                    {
                        try
                        {
                            portedPerLevel = PortCompositesToLevel(source, composites, levelName, overwriteComposites, overwriteAssetsToo, build, out DeadProxyReport deadProxies);
                            deadProxiesPerLevel.Add(new KeyValuePair<string, DeadProxyReport>(levelName, deadProxies));
                        }
                        catch (Exception ex)
                        {
                            //Levels before this one are saved; this one may be part-written. Say so and stop.
                            failedLevel = levelName;
                            failure = ex.Message;
                            return;
                        }
                        written.Add(levelName);
                    }
                });
                while (!work.IsCompleted)
                {
                    Application.DoEvents();
                    Thread.Sleep(16);
                }
                work.GetAwaiter().GetResult();
            }
            finally
            {
                if (OpenCAGE.Undo.UndoStack.Current != null) OpenCAGE.Undo.UndoStack.Current.Blocked = false;
                if (editor != null && !editor.IsDisposed) editor.Enabled = true;
            }
            Cursor.Current = Cursors.Default;

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            if (failedLevel != null)
            {
                string saved = written.Count == 0 ? "No level was written." : "Written and saved: " + string.Join(", ", written) + ".";
                MessageBox.Show("The port failed on " + failedLevel + ":\n\n" + failure + "\n\n" + saved
                    + "\n\n" + failedLevel + " may be part-written on disk - restore it from Manage Backups, or verify the game files.",
                    "Port failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Enabled = true;
                return;
            }

            string count = composites.Count + " composite" + (composites.Count == 1 ? "" : "s") + " (" + portedPerLevel + " including the composites they instance)";
            string destination = targetLevels.Count == 1 ? "'" + targetLevels[0] + "'" : "each of " + targetLevels.Count + " levels";
            string deadProxyText = DeadProxyReport.Describe(deadProxiesPerLevel);
            MessageBox.Show("Finished porting " + count + " to " + destination + "!" + (deadProxyText == "" ? "" : "\n\n" + deadProxyText),
                "Complete", MessageBoxButtons.OK, deadProxyText == "" ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            this.Close();
        }

        /* Runs on the worker: load the destination, port into it, save it. The progress windows are the
           UI thread's, made and closed through CompositeArchive's helpers. */
        private static int PortCompositesToLevel(Level source, List<Composite> composites, string levelName, bool overwriteComposites, bool overwriteAssets, bool build, out DeadProxyReport deadProxies)
        {
            Level lvl = new Level(Singleton.PathToAI + "/DATA/ENV/" + levelName, Singleton.Global, false);
            ProgressUI loadProgress = CompositeArchive.OpenProgress(p => p.ShowLevelLoading(lvl));
            try { lvl.Load(); }
            finally { CompositeArchive.CloseProgress(loadProgress); }

            //The editor's own tables in the destination - pages, modified-parameter marks - do not
            //survive Commands.Save on their own; read them now, write them back after
            LevelEditorTables tables = LevelEditorTables.Read(lvl.Commands.Filepath, lvl.Commands);

            int ported;
            ProgressUI exportProgress = CompositeArchive.OpenProgress(p => p.ShowTransferring("Porting to " + levelName + "..."));
            try
            {
                //The copy itself, and the level data it drags along, is CathodeLib's job; this window only
                //adds what CathodeLib cannot know about - the flowgraph pages for each composite it copies.
                CompositePorter porter = new CompositePorter(source, lvl)
                {
                    OverwriteComposites = overwriteComposites,
                    OverwriteAssets = overwriteAssets,
                    Recurse = true,
                };
                porter.OnProgress = CompositeArchive.RefreshAction(exportProgress);
                porter.OnCompositePorted = (original, copy) =>
                {
                    //Bring over flowgraph layouts (deep-copied; includes predefined fallback), and the
                    //inspector's modified-parameter marks - the destination's table now survives the save,
                    //so a composite without rows in it would read as never modified
                    tables.ReplaceLayouts(original.shortGUID, FlowgraphLayoutManager.GetLayoutsForPort(original));
                    ParameterModificationTracker.ExportCompositeRows(original.shortGUID, tables.Modifications, tables.Defaults);
                };
                foreach (Composite composite in composites)
                    porter.Port(composite);
                ported = porter.PortedComposites.Count;
                //Judged against the destination: what the proxies point at may not exist there
                deadProxies = DeadProxyReport.Of(lvl.Commands, porter.PortedComposites);
            }
            finally
            {
                CompositeArchive.CloseProgress(exportProgress);
            }

            //What the editor's own save does before writing a level: keep pristine copies for the mod tools
            Modding.ModServices.CaptureLevelBeforeSave(levelName);
            //Close alien down if it's open, it conflicts with our write locks!
            EditorUtils.CloseAI();

            /* Commands.Save truncates the script file to the script alone, so once the save has begun the
               editor's tables must go back in whatever happens after - or the level is left saved with every
               page gone. Writing them when nothing was truncated is harmless. */
            try
            {
                ProgressUI saveProgress = CompositeArchive.OpenProgress(p => p.ShowLevelSaving(lvl, build));
                try
                {
                    if (build) lvl.SaveInstanced();
                    else lvl.Save();
                }
                finally
                {
                    CompositeArchive.CloseProgress(saveProgress);
                }
            }
            finally
            {
                //Re-resolved: a level that had only a BIN has a PAK now, and that is what loads next
                try { tables.Write(lvl.CommandsFilepath); }
                catch (Exception e) { Debug.Log("Composite Export", "Could not write the editor tables to " + levelName + ": " + e.Message); }
            }
            return ported;
        }
    }
}

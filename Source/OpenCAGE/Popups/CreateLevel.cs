using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE
{
    /// <summary>
    /// File -> Create Level: make a new, empty level from the shared data every level carries (taken
    /// from FRONTEND, the menu level nothing else edits), optionally port chosen composites (and the
    /// assets they use) from any number of existing levels into it, then open it.
    /// </summary>
    public partial class CreateLevel : BaseWindow
    {
        //New levels live directly under DATA/ENV, so the whole launch-patch budget is theirs
        private static readonly int MaxNameLength = PatchManager.MaxLaunchMapNameLength;

        private CompositeSelection _imports = new CompositeSelection();

        public CreateLevel() : base()
        {
            InitializeComponent();

            //The launcher patches "Production/NAME" into a fixed byte run in the game executable, so a
            //level the game can be launched into has a hard name length
            levelName.MaxLength = MaxNameLength;
            label1.Text = "Level name (up to " + MaxNameLength + " characters):";

            ShowImportSummary();
        }

        private void chooseImports_Click(object sender, EventArgs e)
        {
            using (ImportComposites picker = new ImportComposites(true, null, _imports))
            {
                if (picker.ShowDialog(this) == DialogResult.OK)
                    _imports = picker.Selection;
            }
            ShowImportSummary();
        }

        private void clearImports_Click(object sender, EventArgs e)
        {
            _imports = new CompositeSelection();
            ShowImportSummary();
        }

        private void ShowImportSummary()
        {
            _imports.Prune();
            importSummary.BeginUpdate();
            importSummary.Items.Clear();
            if (_imports.IsEmpty)
            {
                importSummary.Items.Add("(none - the level starts with GLOBAL, PAUSEMENU and the required assets)");
            }
            else
            {
                foreach (CompositeSelection.LevelPick pick in _imports.Levels)
                    importSummary.Items.Add(pick.Level + ":  " + pick.Composites.Count + " composite" + (pick.Composites.Count == 1 ? "" : "s") + (_imports.IncludeChildren ? " (plus what they instance)" : ""));
            }
            importSummary.EndUpdate();
        }

        private void createLevel_Click(object sender, EventArgs e)
        {
            string name = levelName.Text.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show("Please enter a name for the level.", "No name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!Regex.IsMatch(name, "^[A-Za-z0-9_]+$"))
            {
                MessageBox.Show("Level names can only contain letters, digits and underscores.", "Invalid name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (name.Length > MaxNameLength)
            {
                MessageBox.Show("Level names can be at most " + MaxNameLength + " characters, so the game can be launched straight into them.", "Name too long", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string levelId = name.ToUpper();
            string path = Singleton.PathToAI + "/DATA/ENV/" + levelId;
            if (Directory.Exists(path) || Level.GetLevels(Singleton.PathToAI).Contains(levelId))
            {
                MessageBox.Show("A level called '" + name.ToUpper() + "' already exists.", "Level exists", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            //Close alien down if it's open, it conflicts with our write locks!
            EditorUtils.CloseAI();

            Enabled = false;
            Cursor.Current = Cursors.WaitCursor;
            CompositeImporter.Result imported;
            try
            {
                imported = Create(levelId, path);
            }
            catch (Exception ex)
            {
                Cursor.Current = Cursors.Default;
                Enabled = true;
                MessageBox.Show("Failed to create the level:\n\n" + ex.Message, "Create failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Cursor.Current = Cursors.Default;

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            string summary = "Created '" + levelId + "'";
            if (imported.Ported.Count > 0)
                summary += " with " + imported.Ported.Count + " imported composite" + (imported.Ported.Count == 1 ? "" : "s");
            MessageBox.Show(summary + "!", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

            this.Close();
            Singleton.Editor.LoadLevel(levelId);
        }

        private CompositeImporter.Result Create(string levelId, string path)
        {
            //FRONTEND holds the campaign variant of every table a level cannot generate, the smallest Havok
            //scaffolds the game ships, and the GLOBAL/PAUSEMENU/required-asset scripts every level needs -
            //it is the base for every new level.
            Level baseLevel = CompositeImporter.LoadLevel(EditorUtils.FrontendLevel);

            Level newLevel;
            using (ProgressUI progress = new ProgressUI())
            {
                progress.ShowTransferring("Creating " + levelId + "...");
                progress.BringToFront();
                newLevel = Level.MakeNewLevelFrom(path, baseLevel);
                progress.Close();
            }
            baseLevel = null;

            //Flowgraph layouts for everything that lands in the level: imported composites bring their source
            //level's pages, and anything still without one (what came from FRONTEND) takes the bundled
            //predefined pages.
            CompositeFlowgraphTable layouts = (CompositeFlowgraphTable)CustomTable.ReadTable(newLevel.Commands.Filepath, CustomTableType.COMPOSITE_FLOWGRAPHS);
            if (layouts == null) layouts = new CompositeFlowgraphTable();

            CompositeImporter.Result imported = CompositeImporter.Import(_imports, newLevel, (composite, pages) =>
            {
                layouts.flowgraphs.RemoveAll(o => o.CompositeGUID == composite.shortGUID);
                layouts.flowgraphs.AddRange(pages);
            });

            foreach (Composite composite in newLevel.Commands.Entries)
            {
                if (layouts.flowgraphs.Any(o => o.CompositeGUID == composite.shortGUID)) continue;
                layouts.flowgraphs.AddRange(FlowgraphLayoutManager.GetLayoutsForPort(composite, null));
            }

            using (ProgressUI progress = new ProgressUI())
            {
                progress.ShowLevelSaving(newLevel, false);
                progress.BringToFront();
                newLevel.Save();
                progress.Close();
            }
            CustomTable.WriteTable(newLevel.Commands.Filepath, CustomTableType.COMPOSITE_FLOWGRAPHS, layouts);

            //The game finds custom levels through the package list, so register it now rather than at the next launch
            PatchManager.UpdateLevelListInPackages(Singleton.Platform, Singleton.PathToAI);
            return imported;
        }
    }
}

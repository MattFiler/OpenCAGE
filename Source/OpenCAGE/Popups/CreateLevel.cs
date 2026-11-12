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
        //New levels live directly under DATA/ENV, so their launch path is the name alone. The launcher takes 42
        //characters (PatchManager.MaxLaunchMapNameLength), but new levels keep the 32 they were built and tested at.
        private static readonly int MaxNameLength = Math.Min(32, PatchManager.MaxLaunchMapNameLength);

        private CompositeSelection _imports = new CompositeSelection();
        //The imports "Make playable test room" put in the list that were not there already: unticking it takes these out again
        private List<Brushwork.TestRoom.Entry> _testRoomAdded = new List<Brushwork.TestRoom.Entry>();
        private bool _settingTestRoom = false;

        public CreateLevel() : base()
        {
            InitializeComponent();
            OpenCAGE.Popups.Base.BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/create-level/");

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
            //A test room import unticked in the picker: the room can't be made without it, so the option goes off with it
            if (testRoom.Checked && Brushwork.TestRoom.Imports.Any(o => !_imports.Levels.Any(pick => string.Equals(pick.Level, o.Level, StringComparison.OrdinalIgnoreCase) &&
                pick.Composites.Values.Any(name => string.Equals(MCP.McpScript.NormalisePath(name), o.Composite, StringComparison.OrdinalIgnoreCase)))))
                testRoom.Checked = false;
            ShowImportSummary();
        }

        private void clearImports_Click(object sender, EventArgs e)
        {
            _imports = new CompositeSelection();
            _testRoomAdded.Clear();
            testRoom.Checked = false;
            ShowImportSummary();
        }

        private void testRoom_CheckedChanged(object sender, EventArgs e)
        {
            foreach (Control control in new Control[] { roomLights, roomSizeLabel, roomWidth, roomTimes1, roomLength, roomTimes2, roomHeight })
                control.Enabled = testRoom.Checked;
            if (_settingTestRoom)
                return;

            if (testRoom.Checked)
            {
                //The player and the spawn logic, listed with the other imports so it's plain they are coming in
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    _testRoomAdded = Brushwork.TestRoom.AddImports(_imports);
                }
                catch (Exception ex)
                {
                    Cursor.Current = Cursors.Default;
                    MessageBox.Show("A test room can't be made in this install:\n\n" + ex.Message, "Test room", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _settingTestRoom = true;
                    testRoom.Checked = false;
                    _settingTestRoom = false;
                    foreach (Control control in new Control[] { roomLights, roomSizeLabel, roomWidth, roomTimes1, roomLength, roomTimes2, roomHeight })
                        control.Enabled = false;
                }
                Cursor.Current = Cursors.Default;
            }
            else
            {
                //...and out again, unless they were in the list before the option put them there
                Brushwork.TestRoom.RemoveImports(_imports, _testRoomAdded);
                _testRoomAdded.Clear();
            }
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
                //Each level, then the composites ticked in it (what they place comes too, counted in the level's line)
                foreach (CompositeSelection.LevelPick pick in _imports.Levels)
                {
                    importSummary.Items.Add(pick.Level + ":  " + pick.Summary());
                    foreach (string name in pick.Composites.Values.OrderBy(o => o, StringComparer.OrdinalIgnoreCase))
                        importSummary.Items.Add("      " + name + (testRoom.Checked && Brushwork.TestRoom.IsImport(pick.Level, name) ? "   (for the test room)" : ""));
                }
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
            //Also a campaign level's last part ('SCI_HUB'): the two would share its text databases and old-style backups
            string clash = MCP.McpLevels.Clash(levelId, forTools: false);
            if (clash != null)
            {
                MessageBox.Show(clash, "Level exists", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Brushwork.TestRoom.Options room = RoomOptions();
            if (room != null)
            {
                string problem = room.Problem();
                if (problem != null)
                {
                    MessageBox.Show(problem, "Test room", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            //Close alien down if it's open, it conflicts with our write locks! (this install's: another install's game holds none of these files)
            EditorUtils.CloseAI(null, thisInstallOnly: true);

            Enabled = false;
            Cursor.Current = Cursors.WaitCursor;
            List<string> sources = _imports.Levels.Select(o => o.Level).ToList();
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

            //Its subtitles, barks and the imported composites' mission strings: FRONTEND's block is empty, so without a
            //block of its own the level would load only the globals databases and show string ids
            string textWarning = "";
            try
            {
                ConfigEditors.LevelTextDBEditor.WriteNewLevelBlock(levelId, sources);
            }
            catch (Exception ex)
            {
                textWarning = "Its text databases could not be listed in DATA/LEVEL_TEXT_DATABASES.XML (" + ex.Message + "), so its subtitles and barks will show as string ids. " +
                    "Configuration Editors -> Core Game -> Level Text DBs sets them up: Add Standard Set.";
            }
            Cursor.Current = Cursors.Default;

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            Steam.CountTowards(Steam.Achievements.TEN_LEVELS_CREATED);

            string summary = "Created '" + levelId + "'";
            if (imported.Ported.Count > 0)
                summary += " with " + imported.Ported.Count + " imported composite" + (imported.Ported.Count == 1 ? "" : "s");
            string warnings = string.Join("\n\n", new[] { imported.DeadProxies.Describe(levelId), textWarning }.Where(o => !string.IsNullOrEmpty(o)));
            if (room != null)
                summary += "!\n\nIts test room (" + room.Describe() + ") is built once it opens, then the level is saved and built, ready to play";
            MessageBox.Show(summary + "!" + (warnings == "" ? "" : "\n\n" + warnings), "Complete", MessageBoxButtons.OK, warnings == "" ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            this.Close();
            if (Singleton.Editor.LoadLevelAskingToSave(levelId) && room != null)
                Brushwork.TestRoom.BuildWhenOpen(levelId, room);
        }

        /* The test room the options ask for, or null when "Make playable test room" is off */
        private Brushwork.TestRoom.Options RoomOptions()
        {
            if (!testRoom.Checked)
                return null;
            return new Brushwork.TestRoom.Options()
            {
                Width = (float)roomWidth.Value,
                Length = (float)roomLength.Value,
                Height = (float)roomHeight.Value,
                Lights = roomLights.Checked,
            };
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
                progress.KeepOnTop();
                newLevel = Level.MakeNewLevelFrom(path, baseLevel);
                progress.Close();
            }
            baseLevel = null;

            //Flowgraph layouts for everything that lands in the level: imported composites bring their source
            //level's pages, and anything still without one (what came from FRONTEND) takes the bundled
            //predefined pages.
            CompositeFlowgraphTable layouts = (CompositeFlowgraphTable)CustomTable.ReadTable(newLevel.Commands.Filepath, CustomTableType.COMPOSITE_FLOWGRAPHS);
            if (layouts == null) layouts = new CompositeFlowgraphTable();

            //...and their source level's own previews of them, where it took any
            CompositePreviewTable previews = new CompositePreviewTable();
            CompositeImporter.Result imported = CompositeImporter.Import(_imports, newLevel, (composite, pages) =>
            {
                layouts.flowgraphs.RemoveAll(o => o.CompositeGUID == composite.shortGUID);
                layouts.flowgraphs.AddRange(pages);
            }, onArriving: (composite, source) => CompositePreviewManager.CarryInto(previews, composite.shortGUID, source));

            foreach (Composite composite in newLevel.Commands.Entries)
            {
                if (layouts.flowgraphs.Any(o => o.CompositeGUID == composite.shortGUID)) continue;
                layouts.flowgraphs.AddRange(FlowgraphLayoutManager.GetLayoutsForPort(composite, null, EditorUtils.FrontendLevel));
            }

            using (ProgressUI progress = new ProgressUI())
            {
                progress.ShowLevelSaving(newLevel, false);
                progress.KeepOnTop();
                newLevel.Save();
                progress.Close();
            }
            CustomTable.WriteTable(newLevel.Commands.Filepath, CustomTableType.COMPOSITE_FLOWGRAPHS, layouts);
            if (previews.Count != 0)
                CustomTable.WriteTable(newLevel.Commands.Filepath, CustomTableType.COMPOSITE_PREVIEWS, previews);

            //The game finds custom levels through the package list, so register it now rather than at the next launch
            PatchManager.UpdateLevelListInPackages(Singleton.Platform, Singleton.PathToAI);
            return imported;
        }
    }
}

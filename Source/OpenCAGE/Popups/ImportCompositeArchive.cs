using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using OpenCAGE.Popups.Base;
using OpenCAGE.Popups.UserControls;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Bring composites out of an .ocp package into a level. The package's header has already been read
    /// by the time this opens: it lists what the package holds and where it came from, and nothing is
    /// unpacked until the user imports. A package is imported as it is - the tree shows its contents,
    /// there is nothing to pick.
    /// </summary>
    /// <remarks>
    /// Two ways in, decided when the window opens. With a level open in the editor the import goes into
    /// it in memory, like a port from another level, and nothing touches disk until the user saves.
    /// With no level open - a package double-clicked before anything was loaded, say - the window asks
    /// which levels on disk to import into, and each is loaded, written and saved in turn.
    /// </remarks>
    public partial class ImportCompositeArchive : BaseWindow
    {
        private readonly string _archivePath;
        private readonly CompositeArchive.Manifest _manifest;
        private readonly bool _levelOpen;
        //The level the window was built against: a different one underneath it at Import time means a load has happened since
        private readonly LevelContent _contentAtOpen;

        private readonly CompositeTree _tree;
        private readonly LevelPicker _levels;
        //What the window shows: Control.Visible answers for the whole parent chain, so it is false for everything until the window is shown
        private bool _showName, _showDescription, _showWarning, _showDestination;

        public ImportCompositeArchive(string archivePath, CompositeArchive.Manifest manifest) : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            _archivePath = archivePath;
            _manifest = manifest;
            _levelOpen = Content?.IsLevelDataLoaded == true;
            _contentAtOpen = Content;

            InitializeComponent();

            nameLabel.Font = new Font(Font, FontStyle.Bold);
            //The theme paints every control's text colour, including on handle creation; the warning keeps its own
            warningLabel.ForeColor = Color.DarkOrange;
            warningLabel.HandleCreated += (s, e) => warningLabel.ForeColor = Color.DarkOrange;

            _tree = new CompositeTree(compositeTree) { ReadOnly = true };
            _levels = new LevelPicker(levelList, allLevelsButton, noLevelsButton);

            ShowHeader();
            PrepareDestination();
            openAfterImport.Checked = SettingsManager.GetBool(Settings.CompositeImportOpenAfter, false);
            Reflow();
            PopulateComposites();
        }

        #region HEADER
        /* The package's name and description, when its author gave it either, and any reason to doubt the import */
        private void ShowHeader()
        {
            string name = (_manifest.Name ?? "").Trim();
            nameLabel.Text = name;
            nameLabel.Visible = _showName = name.Length != 0;
            toolTip1.SetToolTip(nameLabel, name);

            string description = (_manifest.Description ?? "").Trim();
            descriptionLabel.Text = description;
            descriptionLabel.Visible = _showDescription = description.Length != 0;
            toolTip1.SetToolTip(descriptionLabel, description);

            /* The PC storefronts share one set of level files, so a different one is only worth a
               mention. The other builds do not (see Level.Load's compressed branch), so a package
               from one of them is very unlikely to port cleanly - said plainly, but not refused: the
               user may know better than the manifest. */
            List<string> warnings = new List<string>();
            PatchManager.Platform from = _manifest.ParsedPlatform;
            PatchManager.Platform here = Singleton.Platform;
            if (from != here)
            {
                if (IsPcStorefront(from) && IsPcStorefront(here))
                    warnings.Add("Exported from a " + from + " install (this is " + here + ").");
                else
                    warnings.Add("Exported from a " + from + " install, which does not share level files with this " + here + " install - the import may not work.");
            }
            if (!string.IsNullOrEmpty(_manifest.OpenCAGEVersion) && _manifest.OpenCAGEVersion != Singleton.Version)
                warnings.Add("Exported by OpenCAGE " + _manifest.OpenCAGEVersion + " (this is " + Singleton.Version + ").");

            warningLabel.Text = string.Join("\n", warnings);
            warningLabel.Visible = _showWarning = warnings.Count != 0;
        }

        /// <summary>Where the package came from and what it holds, for the More info button.</summary>
        private string InfoText()
        {
            string name = (_manifest.Name ?? "").Trim();
            int composites = _manifest.Composites.Count(o => o != null);
            int roots = _manifest.Composites.Count(o => o != null && o.IsRoot);

            List<string> lines = new List<string>();
            lines.Add("File:  " + _archivePath);
            if (name.Length != 0)
                lines.Add("Name:  " + name);
            lines.Add("From:  " + (string.IsNullOrEmpty(_manifest.SourceLevel) ? "an unknown level" : _manifest.SourceLevel));
            lines.Add("Exported:  " + (_manifest.ExportedUtc == default(DateTime) ? "unknown" : _manifest.ExportedUtc.ToLocalTime().ToString("f")));
            lines.Add("OpenCAGE:  " + (string.IsNullOrEmpty(_manifest.OpenCAGEVersion) ? "unknown" : _manifest.OpenCAGEVersion)
                + (string.IsNullOrEmpty(_manifest.OpenCAGEBeta) ? "" : " (" + _manifest.OpenCAGEBeta + ")"));
            lines.Add("Platform:  " + (string.IsNullOrEmpty(_manifest.Platform) ? "unknown" : _manifest.Platform));
            lines.Add("Composites:  " + composites
                + (roots != 0 && roots != composites ? " (" + roots + " exported directly, the rest instanced by those)" : ""));
            lines.Add("Files:  " + _manifest.Files.Count(o => o != null));
            lines.Add("Package format:  version " + _manifest.FormatVersion);
            return string.Join("\n", lines);
        }

        private void infoButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show(this, InfoText(), "Package info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static bool IsPcStorefront(PatchManager.Platform platform)
        {
            switch (platform)
            {
                case PatchManager.Platform.STEAM:
                case PatchManager.Platform.EPIC_GAMES_STORE:
                case PatchManager.Platform.GOG:
                case PatchManager.Platform.WINDOWS_STORE:
                    return true;
                default:
                    return false;
            }
        }
        #endregion

        #region LAYOUT
        /* With a level open the import goes into it; otherwise the user ticks levels on disk */
        private void PrepareDestination()
        {
            _showDestination = !_levelOpen;
            if (!_showDestination)
            {
                destinationGroup.Visible = false;
                return;
            }
            //Nothing is opened in the editor after an import into levels on disk: the level itself is offered instead
            openAfterImport.Visible = false;
            toolTip1.SetToolTip(importButton, "Load each ticked level from disk, port the package's composites into it and save it.");
            _levels.Load(EditorUtils.GetEditableLevels());
        }

        /* Stack the window top to bottom around what this package and mode show: a header with no name,
           description or warning closes up, and so does the destination panel when a level is open.
           Labels sit at the margin and the controls under them three pixels in, the way the designer
           lays the other windows out. */
        private void Reflow()
        {
            int margin = Scale(12), inset = Scale(3), gap = Scale(6), block = Scale(10);
            int left = margin + inset;
            int right = ClientSize.Width - margin;
            int y = margin;

            //Name on the left, the info button on the right, on one row
            infoButton.Location = new Point(right - infoButton.Width, y);
            if (_showName)
            {
                nameLabel.AutoSize = false;
                nameLabel.Size = new Size(infoButton.Left - gap - margin, LineHeight(nameLabel));
                nameLabel.Location = new Point(margin, y + (infoButton.Height - nameLabel.Height) / 2);
            }
            y += infoButton.Height + gap;

            if (_showDescription)
            {
                FitText(descriptionLabel, margin, y, right - margin, 4);
                y += descriptionLabel.Height + gap;
            }
            if (_showWarning)
            {
                FitText(warningLabel, margin, y, right - margin, 3);
                y += warningLabel.Height + gap;
            }
            y += block - gap;

            label2.Location = new Point(margin, y);
            y += label2.Height + inset;
            filterBox.Location = new Point(left, y);
            filterBox.Width = right - left;
            y += filterBox.Height + gap;
            compositeTree.Location = new Point(left, y);
            compositeTree.Width = right - left;
            y += compositeTree.Height + gap;
            summaryLabel.Location = new Point(margin, y);
            y += summaryLabel.Height + block;

            if (_showDestination)
            {
                destinationGroup.Location = new Point(left, y);
                destinationGroup.Width = right - left;
                LayoutDestination(margin, inset, gap);
                y += destinationGroup.Height + block;
            }

            overwriteComposites.Location = new Point(left, y);
            overwriteAssets.Location = new Point(left, y + overwriteComposites.Height + gap);
            //Beside the second, in a column clear of both
            openAfterImport.Location = new Point(left + Math.Max(overwriteComposites.Width, overwriteAssets.Width) + block * 2, overwriteAssets.Top);
            importButton.Location = new Point(right - importButton.Width, y);
            y = Math.Max(importButton.Bottom, overwriteAssets.Bottom) + margin;

            ClientSize = new Size(ClientSize.Width, y);
        }

        private void LayoutDestination(int margin, int inset, int gap)
        {
            int left = margin + inset;
            int right = destinationGroup.Width - margin;
            int y = Scale(20);

            levelLabel.Location = new Point(margin, y);
            y += levelLabel.Height + inset;
            levelList.Location = new Point(left, y);
            levelList.Width = right - left;
            y += levelList.Height + gap;
            allLevelsButton.Location = new Point(left, y);
            noLevelsButton.Location = new Point(allLevelsButton.Right + gap, y);
            y += allLevelsButton.Height + gap;
            buildAfterImport.Location = new Point(left, y);
            y += buildAfterImport.Height + margin;

            destinationGroup.Height = y;
        }

        /* Wrap the label's text across the width, up to so many lines; past that it ends in an ellipsis
           (the tooltip has the whole of it) */
        private static void FitText(Label label, int x, int y, int width, int maxLines)
        {
            label.AutoSize = false;
            label.AutoEllipsis = true;
            int wanted = TextRenderer.MeasureText(label.Text, label.Font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak).Height;
            label.Location = new Point(x, y);
            label.Size = new Size(width, Math.Min(wanted, LineHeight(label) * maxLines));
        }

        private static int LineHeight(Label label)
        {
            return TextRenderer.MeasureText("Xg", label.Font).Height;
        }

        /* Designer pixels at 96 dpi, scaled the way the form itself was */
        private int Scale(int pixels)
        {
            return (int)Math.Round(pixels * CurrentAutoScaleDimensions.Height / 13F);
        }
        #endregion

        private void filterBox_TextChanged(object sender, EventArgs e)
        {
            _tree.Filter = filterBox.Text;
        }

        /* Everything the package holds, as the folders its composite names spell out */
        private void PopulateComposites()
        {
            _tree.Load(
                _manifest.Composites.Where(o => o != null).Select(o => new CompositeTree.Item() { Id = new ShortGuid(o.Guid), Name = o.Name, Kind = CompositeTree.KindOf(new ShortGuid(o.Guid), o.Name, false) }),
                null);
            summaryLabel.Text = _tree.Summary();
        }

        /* The composites ticked at export; the porter follows what they instance for itself. A package
           written before roots were recorded is imported whole. */
        private List<uint> CompositesToImport()
        {
            List<uint> roots = _manifest.Composites.Where(o => o != null && o.IsRoot).Select(o => o.Guid).ToList();
            return roots.Count != 0 ? roots : _manifest.Composites.Where(o => o != null).Select(o => o.Guid).ToList();
        }

        private void importButton_Click(object sender, EventArgs e)
        {
            if (_manifest.Composites.Count == 0)
            {
                MessageBox.Show("This package holds no composites.", "Nothing to import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            /* The window was laid out for the level that was (or wasn't) open when it appeared. A load
               started since - the level picker stays open beside this window - means the loaded level is
               not the one this import was aimed at, or a level is being read in under a disk import. */
            //A new LevelContent is made for every load, so a different one is exactly "a load happened"
            if (Singleton.Editor?.IsLevelLoadInProgress == true
                || !ReferenceEquals(Content, _contentAtOpen)
                || (_levelOpen && Content?.IsLevelDataLoaded != true))
            {
                MessageBox.Show("A level has been opened, or is loading, since this window appeared. Open the package again to import it.", "Level changed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
                return;
            }

            SettingsManager.SetBool(Settings.CompositeImportOpenAfter, openAfterImport.Checked);

            CompositeArchive.ImportOptions options = new CompositeArchive.ImportOptions()
            {
                OverwriteComposites = overwriteComposites.Checked,
                OverwriteAssets = overwriteAssets.Checked,
            };

            if (_levelOpen)
                ImportIntoOpenLevel(options);
            else
                ImportIntoLevelsOnDisk(options);
        }

        #region INTO THE OPEN LEVEL
        private void ImportIntoOpenLevel(CompositeArchive.ImportOptions options)
        {
            if (Content?.Level?.Commands == null)
            {
                MessageBox.Show("No level is loaded to import into.", "No level", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Enabled = false;
            Cursor.Current = Cursors.WaitCursor;
            CompositeArchive.Result result;
            List<Composite> ported = new List<Composite>();
            //An import that can replace composites closes the one open in the editor: it is put back afterwards unless an imported one is opened instead
            Composite shown = Singleton.Editor?.CompositeDisplay?.Composite;
            //The viewer follows the import in its script copy only; the rebuild at the end shows it
            Send.BeginSceneBatch();
            try
            {
                result = ImportIntoLoadedLevel(options, ported);
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
                else if (Singleton.Editor?.CompositeDisplay?.Composite == null)
                    CompositeImporter.ReopenClosedComposite(shown);
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
            MessageBox.Show("Imported " + Summarise(result) + ".\n\nSave the level to keep them."
                + (opening == null ? "" : "\n\nThe composite could not be opened in the editor afterwards: " + opening)
                + (deadProxies == "" ? "" : "\n\n" + deadProxies),
                "Complete", MessageBoxButtons.OK, deadProxies == "" ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            Close();
        }

        /* Port straight into the level open in the editor: nothing touches disk until the user saves */
        private CompositeArchive.Result ImportIntoLoadedLevel(CompositeArchive.ImportOptions options, List<Composite> ported)
        {
            Level destination = Content.Level;
            List<uint> ids = CompositesToImport();

            /* With overwrite on, the porter replaces not just the ticked composites but every nested one
               the level already holds. The porter swaps the old copies out of the script directly, so
               nothing that mirrors the level (the viewer above all) would otherwise hear that they went -
               and the viewer holding two registrations of one ID is the shape of bug it has had before.
               A composite about to be replaced may also be open in a tab holding the old object. */
            Dictionary<ShortGuid, Composite> existing = destination.Commands.Entries.Where(o => o != null).GroupBy(o => o.shortGUID).ToDictionary(o => o.Key, o => o.First());
            if (options.OverwriteComposites && _manifest.Composites.Any(o => existing.ContainsKey(new ShortGuid(o.Guid))))
                Singleton.Editor.CompositeBrowser.CloseAllChildTabs();

            Singleton.OnCompositeAddPending?.Invoke();

            CompositeArchive.Result result = CompositeArchive.Import(_archivePath, ids, options, destination, (composite, layouts) =>
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
        #endregion

        #region INTO LEVELS ON DISK
        private void ImportIntoLevelsOnDisk(CompositeArchive.ImportOptions options)
        {
            //A level being backed up must not be rewritten under the backup (the same rule as saving)
            if (Singleton.CurrentBackupState != Singleton.BackupState.NONE)
            {
                MessageBox.Show("Cannot import - a backup is in progress!", "Backup in progress...", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_levels.Count == 0)
            {
                MessageBox.Show("There are no levels to import into.", "Nothing to do", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            List<string> levels = _levels.Selected;
            if (levels.Count == 0)
            {
                MessageBox.Show("Tick the level (or levels) to import into.", "No level selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (levels.Count > 1
                && MessageBox.Show("Import into these " + levels.Count + " levels?\n\n" + string.Join("\n", levels)
                    + "\n\nEach one is loaded, written to and saved in turn, which takes a while" + (buildAfterImport.Checked ? " - and with a build after each, a long while" : "") + ".",
                    "Import into several levels", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            /* Loading, porting and saving a level - let alone building every level - takes minutes, so it
               runs on a worker while this thread pumps messages, the way the editor's own Save and Build
               does: the progress windows paint, and the editor is not reported as hung. Everything that
               could edit or load a level underneath it is held off - the editor window is disabled, the
               level picker closed, the undo stack blocked. */
            CommandsEditor editor = Singleton.Editor;
            List<uint> ids = CompositesToImport();
            bool build = buildAfterImport.Checked;
            Enabled = false;
            Cursor.Current = Cursors.WaitCursor;
            List<CompositeArchive.LevelResult> results;
            editor?.CloseLevelPicker();
            if (editor != null) editor.Enabled = false;
            if (OpenCAGE.Undo.UndoStack.Current != null) OpenCAGE.Undo.UndoStack.Current.Blocked = true;
            try
            {
                Task<List<CompositeArchive.LevelResult>> work = Task.Run(() => CompositeArchive.ImportIntoLevelsOnDisk(_archivePath, ids, options, levels, build));
                while (!work.IsCompleted)
                {
                    Application.DoEvents();
                    Thread.Sleep(16);
                }
                results = work.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Cursor.Current = Cursors.Default;
                Enabled = true;
                MessageBox.Show("The import did not complete:\n\n" + ex.Message + "\n\nLevels written before the failure are saved; the one it failed on may be part-written. The Backup Manager can restore it.", "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                if (OpenCAGE.Undo.UndoStack.Current != null) OpenCAGE.Undo.UndoStack.Current.Blocked = false;
                if (editor != null && !editor.IsDisposed) editor.Enabled = true;
            }
            Cursor.Current = Cursors.Default;

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            CompositeArchive.LevelResult failed = results.FirstOrDefault(o => o.Error != null);
            List<CompositeArchive.LevelResult> done = results.Where(o => o.Result != null).ToList();
            if (failed != null)
            {
                string written = done.Count == 0 ? "No level was written." : "Written and saved: " + string.Join(", ", done.Select(o => o.Level)) + ".";
                MessageBox.Show("The import failed on " + failed.Level + ":\n\n" + failed.Error + "\n\n" + written
                    + "\n\n" + failed.Level + " may be part-written on disk - restore it from Manage Backups, or verify the game files.",
                    "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Enabled = true;
                return;
            }

            if (done.Count == 1)
            {
                string level = done[0].Level;
                string deadProxies = done[0].Result.DeadProxies.Describe(level);
                if (MessageBox.Show("Imported " + Summarise(done[0].Result) + " into " + level + "."
                    + (deadProxies == "" ? "" : "\n\n" + deadProxies)
                    + "\n\nOpen " + level + " now?", "Complete", MessageBoxButtons.YesNo, deadProxies == "" ? MessageBoxIcon.Information : MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    Close();
                    Singleton.Editor?.LoadLevel(level);
                    return;
                }
            }
            else
            {
                int composites = done.Sum(o => o.Result.PortedCount);
                string deadProxies = DeadProxyReport.Describe(done.Select(o => new KeyValuePair<string, DeadProxyReport>(o.Level, o.Result.DeadProxies)));
                MessageBox.Show("Imported into " + done.Count + " levels (" + composites + " composites in all, including the ones they instance)."
                    + (deadProxies == "" ? "" : "\n\n" + deadProxies),
                    "Complete", MessageBoxButtons.OK, deadProxies == "" ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            Close();
        }
        #endregion

        private static string Summarise(CompositeArchive.Result result)
        {
            return result.PortedCount + " composite" + (result.PortedCount == 1 ? "" : "s") + " (" + result.Renderables + " renderables, " + result.CollisionMappings + " collision mappings, " + result.PhysicsSystems + " physics systems)";
        }
    }
}

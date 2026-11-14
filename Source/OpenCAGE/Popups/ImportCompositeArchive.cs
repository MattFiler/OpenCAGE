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
            OpenCAGE.Popups.Base.BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/composite-porting/");

            nameLabel.Font = new Font(Font, FontStyle.Bold);
            //The theme paints every control's text colour, including on handle creation; the warning keeps its own
            warningLabel.ForeColor = Color.DarkOrange;
            warningLabel.HandleCreated += (s, e) => warningLabel.ForeColor = Color.DarkOrange;

            _tree = new CompositeTree(compositeTree) { ReadOnly = true };
            _tree.AttachSearch(filterBox, searchButton);
            _levels = new LevelPicker(levelList, allLevelsButton, noLevelsButton);

            ShowHeader();
            PrepareDestination();
            PrepareEditedList();
            openAfterImport.Checked = SettingsManager.GetBool(Settings.CompositeImportOpenAfter, false);
            Reflow();
            PopulateComposites();
        }

        #region YOUR EDITS
        /* The composites the user has changed in the destination (its edit history says so) that overwriting would replace
           with the package's copies: listed under the overwrite box, unticked - kept - unless the user ticks one, or "Replace
           them anyway". */
        private Label _editedLabel;
        private CheckedListBox _editedList;
        private CheckBox _replaceAnyway;
        private bool _showEdited;
        private bool _settingEditedTicks;
        private bool _laidOut;
        //Each level on disk's edited composites, by its script's write time (ticking levels re-reads only what changed)
        private readonly Dictionary<string, KeyValuePair<DateTime, List<CompositeArchive.EditedComposite>>> _editedOnDisk = new Dictionary<string, KeyValuePair<DateTime, List<CompositeArchive.EditedComposite>>>(StringComparer.OrdinalIgnoreCase);

        private class EditedRow
        {
            public uint Guid;
            public string Name;
            public DateTime Edited;
            public List<string> Levels = new List<string>();
            public override string ToString() => Name + "   (you changed it " + Edited.ToString("g") + (Levels.Count == 0 ? "" : " in " + string.Join(", ", Levels)) + ")";
        }

        private void PrepareEditedList()
        {
            _editedLabel = new Label() { AutoSize = false, ForeColor = Color.DarkOrange };
            _editedLabel.HandleCreated += (s, e) => _editedLabel.ForeColor = Color.DarkOrange;
            _editedList = new CheckedListBox() { CheckOnClick = true, IntegralHeight = false, HorizontalScrollbar = true };
            _replaceAnyway = new CheckBox() { AutoSize = true, Text = "Replace them anyway" };
            toolTip1.SetToolTip(_editedList, "Composites you have changed here that the package also holds. Unticked: your version stays (the import still brings in what it adds around it). Ticked: the package's copy replaces yours.");
            toolTip1.SetToolTip(_replaceAnyway, "Tick to let the package replace every composite listed, losing your changes to them.");
            toolTip1.SetToolTip(overwriteComposites, "If checked: a composite the level already holds under the same ID is replaced by the imported copy - except ones you have changed here, which are listed below it to choose from. If unchecked, every existing one is kept.");
            Controls.AddRange(new Control[] { _editedLabel, _editedList, _replaceAnyway });
            _editedList.ItemCheck += (s, e) =>
            {
                if (_settingEditedTicks) return;
                //ItemCheck comes before the tick changes
                BeginInvoke(new Action(SyncReplaceAnyway));
            };
            _replaceAnyway.CheckedChanged += (s, e) =>
            {
                if (_settingEditedTicks) return;
                _settingEditedTicks = true;
                for (int i = 0; i < _editedList.Items.Count; i++)
                    _editedList.SetItemChecked(i, _replaceAnyway.Checked);
                _settingEditedTicks = false;
            };
            overwriteComposites.CheckedChanged += (s, e) => RefreshEdited();
            _levels.SelectionChanged += RefreshEdited;
            RefreshEdited();
        }

        private void SyncReplaceAnyway()
        {
            if (IsDisposed) return;
            _settingEditedTicks = true;
            _replaceAnyway.Checked = _editedList.Items.Count != 0 && _editedList.CheckedItems.Count == _editedList.Items.Count;
            _settingEditedTicks = false;
        }

        /* Who has what: the open level's history, or each ticked level on disk's */
        private void RefreshEdited()
        {
            List<EditedRow> rows = new List<EditedRow>();
            if (overwriteComposites.Checked)
            {
                try
                {
                    if (_levelOpen)
                    {
                        if (Content?.Level?.Commands != null)
                            rows.AddRange(CompositeArchive.EditedCompositesItWouldReplace(_manifest, Content.Level.Commands).Select(o => new EditedRow() { Guid = o.Guid, Name = o.Name, Edited = o.Edited }));
                    }
                    else
                    {
                        Dictionary<uint, EditedRow> byId = new Dictionary<uint, EditedRow>();
                        foreach (string level in _levels.Selected)
                            foreach (CompositeArchive.EditedComposite edited in EditedOnDisk(level))
                            {
                                if (!byId.TryGetValue(edited.Guid, out EditedRow row))
                                    byId[edited.Guid] = row = new EditedRow() { Guid = edited.Guid, Name = edited.Name, Edited = edited.Edited };
                                row.Levels.Add(level);
                                if (edited.Edited > row.Edited) row.Edited = edited.Edited;
                            }
                        rows.AddRange(byId.Values.OrderByDescending(o => o.Edited));
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log("Import", "Could not read the destination's edit history: " + ex.Message);
                }
            }

            //A row still listed keeps the tick it had
            HashSet<uint> ticked = new HashSet<uint>(_editedList.CheckedItems.OfType<EditedRow>().Select(o => o.Guid));
            _settingEditedTicks = true;
            _editedList.BeginUpdate();
            _editedList.Items.Clear();
            foreach (EditedRow row in rows)
                _editedList.Items.Add(row, ticked.Contains(row.Guid));
            _editedList.EndUpdate();
            _settingEditedTicks = false;
            SyncReplaceAnyway();

            _editedLabel.Text = "You have changed " + (rows.Count == 1 ? "1 composite" : rows.Count + " composites") + (_levelOpen ? " in this level" : "")
                + " that this package would replace. Unticked ones keep your version; tick one to take the package's copy instead:";
            _showEdited = rows.Count != 0;
            _editedLabel.Visible = _editedList.Visible = _replaceAnyway.Visible = _showEdited;
            if (_laidOut)
                Reflow();
        }

        private List<CompositeArchive.EditedComposite> EditedOnDisk(string level)
        {
            string commands = new Level(Singleton.PathToAI + "/DATA/ENV/" + level, Singleton.Global, false).CommandsFilepath;
            DateTime written = File.Exists(commands) ? File.GetLastWriteTimeUtc(commands) : DateTime.MinValue;
            if (_editedOnDisk.TryGetValue(level, out var cached) && cached.Key == written)
                return cached.Value;
            List<CompositeArchive.EditedComposite> edited = CompositeArchive.EditedCompositesItWouldReplace(_manifest, level);
            _editedOnDisk[level] = new KeyValuePair<DateTime, List<CompositeArchive.EditedComposite>>(written, edited);
            return edited;
        }

        /* The ticked rows: replaced all the same */
        private HashSet<uint> EditedToReplace()
        {
            return new HashSet<uint>(_editedList.CheckedItems.OfType<EditedRow>().Select(o => o.Guid));
        }

        private static string KeptText(CompositeArchive.Result result)
        {
            if (result?.KeptEdited == null || result.KeptEdited.Count == 0) return "";
            return "\n\nKept your version of " + (result.KeptEdited.Count == 1 ? "1 composite" : result.KeptEdited.Count + " composites") + " you had changed: "
                + string.Join(", ", result.KeptEdited.Take(8)) + (result.KeptEdited.Count > 8 ? ", ..." : "") + ".";
        }
        #endregion

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
            //Which OpenCAGE exported it says nothing on its own (More info shows it): only a format flagged as not importing
            //exactly is warned about
            string caveat = CompositeArchive.FormatCaveat(_manifest.FormatVersion);
            if (caveat != null)
                warnings.Add(caveat);

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
            //The box, and its Search button at the end of the row
            searchButton.Location = new Point(right - searchButton.Width, y + (filterBox.Height - searchButton.Height) / 2);
            filterBox.Location = new Point(left, y);
            filterBox.Width = searchButton.Left - gap - left;
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
            int boxesTop = y;
            y += overwriteComposites.Height + gap;
            //The user's edited composites overwriting would replace, under the box they belong to, clear of the Import button
            if (_showEdited)
            {
                int indent = left + Scale(16);
                int width = right - importButton.Width - gap - indent;
                FitText(_editedLabel, indent, y, width, 3);
                y += _editedLabel.Height + inset;
                _editedList.Location = new Point(indent, y);
                _editedList.Size = new Size(width, _editedList.ItemHeight * Math.Max(2, Math.Min(6, _editedList.Items.Count)) + Scale(4));
                y += _editedList.Height + inset;
                _replaceAnyway.Location = new Point(indent, y);
                y += _replaceAnyway.Height + gap;
            }
            overwriteAssets.Location = new Point(left, y);
            //Beside the second, in a column clear of both
            openAfterImport.Location = new Point(left + Math.Max(overwriteComposites.Width, overwriteAssets.Width) + block * 2, overwriteAssets.Top);
            importButton.Location = new Point(right - importButton.Width, _showEdited ? overwriteAssets.Bottom - importButton.Height : boxesTop);
            y = Math.Max(importButton.Bottom, overwriteAssets.Bottom) + margin;

            ClientSize = new Size(ClientSize.Width, y);
            _laidOut = true;
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
                //Composites the user changed in the destination stay as they are, but the ones ticked to replace anyway
                KeepEdited = true,
                ReplaceEdited = EditedToReplace(),
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
            //An import that can replace composites closes the one open in the editor: the user is put back where they were (stepped down
            //through the same instances, the same selection) afterwards, unless an imported one is opened instead
            CompositePath.Place shown = Singleton.Editor?.CompositeDisplay?.CapturePlace();
            //The viewer follows the import in its script copy only; the rebuild at the end shows it
            Send.BeginSceneBatch();
            try
            {
                //Minutes for a big package: the editor is held still meanwhile, still painting (see EditorBusy)
                using (EditorBusy.Begin())
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
                    CompositeImporter.ReopenClosedPlace(shown);
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
                + KeptText(result)
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
                //A package carries no previews. The composites it was made from are most likely edited copies, so they are taken
                //afresh at the next save; what they place keeps the preview its ID has, as a port's does
                if (_manifest.Composites.Any(o => o.IsRoot && o.Guid == composite.shortGUID.AsUInt32))
                    CompositePreviewManager.MarkEdited(composite);
                FlowgraphLayoutManager.ImportLayouts(composite, layouts);
                EditHistory.CompositesChanged(destination.Commands, new[] { composite.shortGUID });
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
                MessageBox.Show("Cannot import - " + Singleton.BusyText.ToLowerInvariant() + "!", "Please wait...", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
               could edit or load a level underneath it is held off meanwhile (see EditorBusy). */
            List<uint> ids = CompositesToImport();
            bool build = buildAfterImport.Checked;
            Enabled = false;
            Cursor.Current = Cursors.WaitCursor;
            List<CompositeArchive.LevelResult> results;
            try
            {
                using (EditorBusy.Begin())
                    results = EditorBusy.Run(() => CompositeArchive.ImportIntoLevelsOnDisk(_archivePath, ids, options, levels, build));
            }
            catch (Exception ex)
            {
                Cursor.Current = Cursors.Default;
                Enabled = true;
                MessageBox.Show("The import did not complete:\n\n" + ex.Message + "\n\nLevels written before the failure are saved; the one it failed on may be part-written. The Backup Manager can restore it.", "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
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
                    + (failed.Untouched ? "" : "\n\n" + failed.Level + " may be part-written on disk - restore it from Manage Backups, or verify the game files."),
                    "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Enabled = true;
                return;
            }

            if (done.Count == 1)
            {
                string level = done[0].Level;
                string deadProxies = done[0].Result.DeadProxies.Describe(level);
                if (MessageBox.Show("Imported " + Summarise(done[0].Result) + " into " + level + "."
                    + KeptText(done[0].Result)
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
                int kept = done.Sum(o => o.Result.KeptEdited.Count);
                MessageBox.Show("Imported into " + done.Count + " levels (" + composites + " composites in all, including the ones they instance)."
                    + (kept == 0 ? "" : "\n\nKept your version of " + kept + " composite" + (kept == 1 ? "" : "s") + " you had changed (" + string.Join("; ", done.Where(o => o.Result.KeptEdited.Count != 0).Select(o => o.Level + ": " + string.Join(", ", o.Result.KeptEdited.Take(4)) + (o.Result.KeptEdited.Count > 4 ? ", ..." : ""))) + ").")
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

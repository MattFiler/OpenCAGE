using CathodeLib;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

namespace OpenCAGE.ConfigEditors
{
    /// <summary>
    /// DATA/LEVEL_TEXT_DATABASES.XML and each level's own TEXT/TEXT_DB_LIST.TXT - which text databases a
    /// level loads, and so which localised strings its script can display.
    ///
    /// The two sources are ADDITIVE, not alternatives: the engine reads
    /// <c>data/level_text_databases.xml</c> for the shared databases under DATA/TEXT (the level's own
    /// block plus the "globals" block every level gets), and <c>data/env/&lt;level&gt;/text/text_db_list.txt</c>
    /// for databases shipped inside the level folder. A level ends up with the union, which is what
    /// <see cref="Level.Load"/> resolves.
    ///
    /// The XML keys a level by its folder name only ("BSP_Torrens"), not its path, so two levels with the
    /// same folder name in different subfolders would share an entry.
    /// </summary>
    public partial class LevelTextDBEditor : BaseWindow
    {
        private const string GlobalsEntry = "globals";
        private const string GlobalsDisplay = "(globals - every level)";
        private const string DbListFile = "/TEXT/TEXT_DB_LIST.TXT";
        private const string MissingSuffix = "   (file missing)";

        private class LevelEntry
        {
            public string Display;      //what the list shows
            public string Path;         //level path under DATA/ENV, or null for the globals block
            public string XmlName;      //the name the XML keys this level by
            public bool IsGlobals => Path == null;
        }

        private readonly List<LevelEntry> _levels = new List<LevelEntry>();
        private readonly List<string> _sharedDbs = new List<string>();
        private XmlDocument _config;
        private string _configPath;
        private LevelEntry _selected;
        private bool _populating;
        private bool _loadFailed;

        public LevelTextDBEditor() : base()
        {
            InitializeComponent();

            _configPath = Singleton.PathToAI + "/DATA/LEVEL_TEXT_DATABASES.XML";
            try
            {
                _config = new XmlDocument();
                //Keep the file's own indentation, so checking one database doesn't rewrite every line
                _config.PreserveWhitespace = true;
                _config.Load(_configPath);
            }
            catch (Exception e)
            {
                Debug.Log("Level Text DBs", "Failed to read " + _configPath + ": " + e.Message);
                _config = null;
            }

            if (_config?["level_text_databases"] == null)
            {
                MessageBox.Show("Could not read DATA/LEVEL_TEXT_DATABASES.XML.\n\nUse Options -> Manage Game Directories to point OpenCAGE at a valid install.",
                    "Level text databases missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _loadFailed = true;
                return;
            }

            ConfigEditorUtils.ShowAutoSaveTipOnce();

            //Every database name that exists under DATA/TEXT, in any language
            string sharedText = Singleton.PathToAI + "/DATA/TEXT";
            if (Directory.Exists(sharedText))
            {
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string file in Directory.GetFiles(sharedText, "*.TXT", SearchOption.AllDirectories))
                    if (seen.Add(Path.GetFileNameWithoutExtension(file)))
                        _sharedDbs.Add(Path.GetFileNameWithoutExtension(file));
                _sharedDbs.Sort(StringComparer.OrdinalIgnoreCase);
            }

            _levels.Add(new LevelEntry() { Display = GlobalsDisplay, Path = null, XmlName = GlobalsEntry });
            foreach (string level in EditorUtils.GetEditableLevels())
                _levels.Add(new LevelEntry() { Display = level, Path = level, XmlName = level.Replace('\\', '/').Split('/').Last() });

            levelList.BeginUpdate();
            foreach (LevelEntry level in _levels)
                levelList.Items.Add(level.Display);
            levelList.EndUpdate();

            //Clicking a row ticks it as well as selecting it, so the edit buttons follow the selection
            sharedDbList.SelectedIndexChanged += (s, ev) => UpdateButtonStates();
            localDbList.SelectedIndexChanged += (s, ev) => UpdateButtonStates();

            string current = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Name;
            int index = current == null ? -1 : _levels.FindIndex(o => string.Equals(o.Path, current, StringComparison.OrdinalIgnoreCase));
            levelList.SelectedIndex = index >= 0 ? index : 0;

            Singleton.OnResetConfigs += OnResetConfigs;
            FormClosed += (s, e) => { Singleton.OnResetConfigs -= OnResetConfigs; };
        }

        private string SharedTextFolder => Singleton.PathToAI + "/DATA/TEXT";

        private string LocalTextFolder(LevelEntry level)
        {
            return Singleton.PathToAI + "/DATA/ENV/" + level.Path + "/TEXT";
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_loadFailed)
                Close();
        }

        private void OnResetConfigs()
        {
            Close();
        }

        private void levelList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (levelList.SelectedIndex < 0 || levelList.SelectedIndex >= _levels.Count)
                return;

            _selected = _levels[levelList.SelectedIndex];
            _populating = true;

            //Shared databases: ticked when the level's block in the XML names them
            List<string> configured = ReadXmlDbs(_selected.XmlName).ToList();
            HashSet<string> inXml = new HashSet<string>(configured, StringComparer.OrdinalIgnoreCase);
            sharedDbList.BeginUpdate();
            sharedDbList.Items.Clear();
            foreach (string db in _sharedDbs)
                sharedDbList.Items.Add(db, inXml.Contains(db));

            //The config can name a database DATA/TEXT doesn't hold (retail names CV0, which does not exist).
            //It has to stay in the list, ticked: leaving it out would drop it from the file on the next save.
            List<string> missing = configured.Where(o => !_sharedDbs.Contains(o, StringComparer.OrdinalIgnoreCase)).ToList();
            foreach (string db in missing)
                sharedDbList.Items.Add(db + MissingSuffix, true);
            sharedDbList.EndUpdate();

            missingLabel.Text = missing.Count == 0
                ? ""
                : "Named in the config but not in DATA/TEXT: " + string.Join(", ", missing) + ". Untick to drop.";

            //Level-local databases: the level's own TEXT folder, ticked when TEXT_DB_LIST.TXT names them
            localDbList.BeginUpdate();
            localDbList.Items.Clear();
            if (_selected.IsGlobals)
            {
                localGroup.Enabled = false;
                localHint.Text = "The globals block applies to every level, so it has no level folder of its own.";
            }
            else
            {
                localGroup.Enabled = true;
                string textFolder = Singleton.PathToAI + "/DATA/ENV/" + _selected.Path + "/TEXT";
                if (!Directory.Exists(textFolder))
                {
                    localHint.Text = "This level ships no strings of its own yet. Use New Database to add one.";
                }
                else
                {
                    HashSet<string> listed = new HashSet<string>(ReadLocalDbList(_selected), StringComparer.OrdinalIgnoreCase);
                    HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    List<string> found = new List<string>();
                    foreach (string file in Directory.GetFiles(textFolder, "*.TXT", SearchOption.AllDirectories))
                    {
                        string db = Path.GetFileNameWithoutExtension(file);
                        if (string.Equals(db, "TEXT_DB_LIST", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (seen.Add(db))
                            found.Add(db);
                    }
                    found.Sort(StringComparer.OrdinalIgnoreCase);
                    foreach (string db in found)
                        localDbList.Items.Add(db, listed.Contains(db));

                    //Anything listed but no longer in the folder still belongs in the list box, unticked would lose it silently
                    foreach (string db in listed.Where(o => !seen.Contains(o)))
                        localDbList.Items.Add(db + MissingSuffix, true);

                    localHint.Text = found.Count == 0
                        ? "The TEXT folder holds no databases yet. Use New Database to add one."
                        : "Databases in DATA/ENV/" + _selected.Path + "/TEXT. Ticked ones are listed in TEXT_DB_LIST.TXT.";
                }
            }
            localDbList.EndUpdate();

            _populating = false;
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            editSharedDb.Enabled = sharedDbList.SelectedIndex >= 0;
            editLocalDb.Enabled = localDbList.SelectedIndex >= 0;
        }

        private IEnumerable<string> ReadXmlDbs(string xmlName)
        {
            XmlElement block = FindLevelBlock(xmlName);
            if (block == null)
                return new List<string>();
            return block.ChildNodes.OfType<XmlElement>()
                .Where(o => o.Name == "text_database" && o.GetAttribute("name").Length != 0)
                .Select(o => o.GetAttribute("name"))
                .ToList();
        }

        private XmlElement FindLevelBlock(string xmlName)
        {
            return _config["level_text_databases"].ChildNodes.OfType<XmlElement>()
                .FirstOrDefault(o => o.Name == "level" && string.Equals(o.GetAttribute("name"), xmlName, StringComparison.OrdinalIgnoreCase));
        }

        private IEnumerable<string> ReadLocalDbList(LevelEntry level)
        {
            string path = Singleton.PathToAI + "/DATA/ENV/" + level.Path + DbListFile;
            if (!File.Exists(path))
                return new List<string>();
            try
            {
                return File.ReadAllLines(path).Select(o => o.Trim()).Where(o => o.Length != 0).ToList();
            }
            catch (Exception e)
            {
                Debug.Log("Level Text DBs", "Failed to read " + path + ": " + e.Message);
                return new List<string>();
            }
        }

        /* ItemCheck runs BEFORE the box updates, so the item being changed reads from the event and the
         * rest from the box. Saving here rather than posting the save back to the form matters: a tick
         * followed straight away by closing the window would lose a posted save with the form. */
        private static List<string> CheckedNames(CheckedListBox list, ItemCheckEventArgs e)
        {
            List<string> names = new List<string>();
            for (int i = 0; i < list.Items.Count; i++)
            {
                bool ticked = i == e.Index ? e.NewValue == CheckState.Checked : list.GetItemChecked(i);
                if (ticked)
                    names.Add(list.Items[i].ToString());
            }
            return names;
        }

        private void sharedDbList_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (_populating || _selected == null)
                return;
            SaveXml(CheckedNames(sharedDbList, e));
        }

        private void localDbList_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (_populating || _selected == null || _selected.IsGlobals)
                return;
            SaveLocalDbList(CheckedNames(localDbList, e));
        }

        private void SaveXml(List<string> databases)
        {
            if (_selected == null)
                return;

            XmlElement root = _config["level_text_databases"];
            XmlElement block = FindLevelBlock(_selected.XmlName);

            //The list shows names as the files on disk spell them, and marks the ones with no file at all;
            //the file only wants the name
            databases = databases.Select(o => o.Split(new[] { MissingSuffix }, StringSplitOptions.None)[0]).ToList();

            //The config spells several databases differently from their files ("Cutscenes" against
            //CUTSCENES.TXT). Both work - the engine and the loader match case-insensitively - so keep the
            //spelling and the order already in the file, and only append what is genuinely new.
            List<string> configured = ReadXmlDbs(_selected.XmlName).ToList();
            HashSet<string> wanted = new HashSet<string>(databases, StringComparer.OrdinalIgnoreCase);
            List<string> ordered = configured.Where(o => wanted.Contains(o)).ToList();
            HashSet<string> kept = new HashSet<string>(ordered, StringComparer.OrdinalIgnoreCase);
            ordered.AddRange(databases.Where(o => !kept.Contains(o)));
            databases = ordered;

            if (databases.Count == 0)
            {
                //No databases left: drop the block rather than leave an empty one behind
                if (block != null)
                {
                    if (block.PreviousSibling != null && block.PreviousSibling.NodeType == XmlNodeType.Whitespace)
                        root.RemoveChild(block.PreviousSibling);
                    root.RemoveChild(block);
                }
            }
            else
            {
                //A block already there keeps the name it was written with - the config spells several levels
                //differently from their folders ("BSP_Torrens"), and both work
                string blockName = block?.GetAttribute("name") ?? _selected.XmlName;
                if (block == null)
                {
                    block = _config.CreateElement("level");
                    block.SetAttribute("name", blockName);
                    ConfigEditorUtils.AppendIndented(root, block, "\r\n\t");
                }

                //Rewrite this level's entries in place; every other level's block is untouched
                List<XmlNode> entries = new List<XmlNode>();
                foreach (string db in databases)
                {
                    XmlElement entry = _config.CreateElement("text_database");
                    entry.SetAttribute("name", db);
                    entries.Add(entry);
                }
                ConfigEditorUtils.ReplaceChildrenIndented(block, entries, "\r\n\t\t", "\r\n\t");
                block.SetAttribute("name", blockName); //ReplaceChildrenIndented clears attributes
            }

            try
            {
                Modding.ModServices.CaptureBeforeWrite(_configPath);
                _config.Save(_configPath);
                ConfigEditorUtils.NotifyAutoSave(true);
            }
            catch (Exception ex)
            {
                ConfigEditorUtils.NotifyAutoSave(false, ex.Message);
                return;
            }

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void SaveLocalDbList(List<string> checkedNames)
        {
            if (_selected == null || _selected.IsGlobals)
                return;

            //The display carries a "(file missing)" note for databases the folder no longer holds - the file only wants names
            List<string> databases = checkedNames
                .Select(o => o.Split(new[] { MissingSuffix }, StringSplitOptions.None)[0])
                .ToList();

            string path = Singleton.PathToAI + "/DATA/ENV/" + _selected.Path + DbListFile;
            try
            {
                if (databases.Count == 0)
                {
                    if (File.Exists(path))
                    {
                        Modding.ModServices.CaptureBeforeWrite(path);
                        File.Delete(path);
                    }
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    Modding.ModServices.CaptureBeforeWrite(path);
                    File.WriteAllLines(path, databases);
                }
                ConfigEditorUtils.NotifyAutoSave(true);
            }
            catch (Exception ex)
            {
                ConfigEditorUtils.NotifyAutoSave(false, ex.Message);
                return;
            }

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private static string StripMissing(string display)
        {
            return display.Split(new[] { MissingSuffix }, StringSplitOptions.None)[0];
        }

        private void newLocalDb_Click(object sender, EventArgs e)
        {
            if (_selected == null || _selected.IsGlobals)
                return;

            string name = null;
            using (RenameGeneric prompt = new RenameGeneric("", new RenameGeneric.RenameGenericContent()
            {
                Title = "New Text Database",
                Description = "Name for the new database in " + _selected.Path + " - it is created in all nine languages:",
                ButtonText = "Create"
            }))
            {
                prompt.OnRenamed += (n) => name = n;
                prompt.ShowDialog(this);
            }

            if (string.IsNullOrWhiteSpace(name))
                return;
            name = name.Trim();

            //The level's own files are read after the shared ones and win, so a level database named after a
            //shared one replaces it for this level. That is a legitimate way to override a shared bank, but
            //it is rarely what someone reaching for "new database" means to do.
            if (_sharedDbs.Contains(name, StringComparer.OrdinalIgnoreCase) &&
                MessageBox.Show(this,
                    "DATA/TEXT already has a database called \"" + name + "\".\n\n" +
                    "A database of that name inside the level replaces the shared one for this level. Create it anyway?",
                    "Name already used", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            if (!LocalisationHandler.TryCreateDatabase(LocalTextFolder(_selected), name, out string error))
            {
                MessageBox.Show(this, error, "Could not create database", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            //List it straight away - a database the level does not list is one the game never reads
            List<string> listed = new List<string>();
            foreach (object item in localDbList.CheckedItems)
                listed.Add(StripMissing(item.ToString()));
            listed.Add(name);
            SaveLocalDbList(listed);

            levelList_SelectedIndexChanged(levelList, EventArgs.Empty);
            for (int i = 0; i < localDbList.Items.Count; i++)
            {
                if (string.Equals(StripMissing(localDbList.Items[i].ToString()), name, StringComparison.OrdinalIgnoreCase))
                {
                    localDbList.SelectedIndex = i;
                    break;
                }
            }
        }

        /* One contents editor at a time: the button is easy to hit twice, and two editors over the same files
         * would each write over the other's work. */
        private LocalisationEditor _contentsEditor = null;

        private void OpenContentsEditor(CheckedListBox list, string textFolder, string scope)
        {
            if (list.SelectedIndex < 0)
                return;

            string display = list.Items[list.SelectedIndex].ToString();
            string db = StripMissing(display);
            if (display.Length != db.Length)
            {
                MessageBox.Show(this, "\"" + db + "\" is listed but has no file to edit.",
                    "Nothing to edit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_contentsEditor != null)
            {
                _contentsEditor.FormClosed -= ContentsEditorClosed;
                _contentsEditor.Close();
                _contentsEditor = null;
            }

            _contentsEditor = new LocalisationEditor(textFolder, db, scope + " / " + db);
            _contentsEditor.FormClosed += ContentsEditorClosed;
            _contentsEditor.Show();
        }

        private void ContentsEditorClosed(object sender, FormClosedEventArgs e)
        {
            _contentsEditor = null;
        }

        private void editSharedDb_Click(object sender, EventArgs e)
        {
            OpenContentsEditor(sharedDbList, SharedTextFolder, "DATA/TEXT");
        }

        private void editLocalDb_Click(object sender, EventArgs e)
        {
            if (_selected == null || _selected.IsGlobals)
                return;
            OpenContentsEditor(localDbList, LocalTextFolder(_selected), _selected.Path);
        }
    }
}

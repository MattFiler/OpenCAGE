using CathodeLib;
using CommandsEditor.Popups.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Xml.XPath;
using WeifenLuo.WinFormsUI.Docking;

namespace CommandsEditor.ConfigEditors
{
    public partial class LevelTextDBEditor : BaseWindow
    {
        //todo - this is OLD and needs updating!!!

        HashSet<string> _availableGlobalDBs = new HashSet<string>();
        List<Level> _levels = new List<Level>();
        Level _selectedLevel = null;

        public LevelTextDBEditor() : base()
        {
            InitializeComponent();

            ReadLevelTextDatabasesXML();
            ReadLocalTextDBs();

            _levels = _levels.OrderBy(o => o.name).ToList();

            string[] textPaths = Directory.GetFiles(Singleton.PathToAI + "/DATA/TEXT", "*.TXT", SearchOption.AllDirectories);
            foreach (string path in textPaths)
                _availableGlobalDBs.Add(Path.GetFileNameWithoutExtension(path));

            levelList.BeginUpdate();
            foreach (Level lvl in _levels)
                levelList.Items.Add(lvl.name);
            levelList.EndUpdate();

            Singleton.OnResetConfigs += () => { this.Close(); };
        }

        private void levelList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (levelList.SelectedItem == null)
                return;

            if (_selectedLevel != null)
            {
                _selectedLevel.text_dbs.Clear();
                for (int i = 0; i < textDbList.Items.Count; i++)
                {
                    if (!textDbList.GetItemChecked(i))
                        continue;
                    _selectedLevel.text_dbs.Add(textDbList.Items[i].ToString());
                }
            }

            _selectedLevel = _levels.FirstOrDefault(o => o.name == levelList.SelectedItem.ToString());

            textDbList.BeginUpdate();
            textDbList.Items.Clear();
            if (_selectedLevel.using_local_db)
            {
                foreach (string db in _selectedLevel.available_local_dbs)
                    textDbList.Items.Add(db);
            }
            else
            {
                foreach (string db in _availableGlobalDBs)
                    textDbList.Items.Add(db);
            }
            textDbList.EndUpdate();

            for (int i = 0; i < textDbList.Items.Count; i++)
            {
                textDbList.SetItemChecked(i, _selectedLevel.text_dbs.Contains(textDbList.Items[i].ToString()));
            }

            useLocalTextDBs.Checked = _selectedLevel.using_local_db;
        }

        private void SaveBtn_Click(object sender, EventArgs e)
        {
            WriteLevelTextDatabasesXML();
            WriteLocalTextDBs();

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void ReadLevelTextDatabasesXML()
        {
            _levels.Clear();

            var xml = XDocument.Load(Singleton.PathToAI + "/DATA/level_text_databases.xml");
            IEnumerable<XElement> levelDBs = xml.XPathSelectElements("//level_text_databases/level");
            foreach (XElement levelDB in levelDBs)
            {
                string levelName = levelDB.Attribute("name").Value.ToString().ToUpper();

                //Some levels referenced in the vanilla config don't exist in retail - ignore them
                if (levelName != "globals" && !Directory.Exists(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/" + levelName) && !Directory.Exists(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/DLC/" + levelName))
                {
                    Console.WriteLine("Skipping deleted level: " + levelName);
                    continue;
                }

                Level level = new Level();
                level.name = levelName;
                var databases = levelDB.XPathSelectElements("text_database");
                foreach (XElement database in databases)
                {
                    level.text_dbs.Add(database.Attribute("name").Value.ToString());
                }
                _levels.Add(level);
            }
        }

        private void ReadLocalTextDBs()
        {
            List<string> levelFolders = new List<string>();
            {
                string[] levels = Directory.GetDirectories(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/");
                for (int i = 0; i < levels.Length; i++)
                {
                    string dirName = Path.GetFileName(levels[i]);
                    if (dirName == "DLC")
                        continue;
                    levelFolders.Add(dirName.ToUpper());
                }
            }
            {
                string[] levels = Directory.GetDirectories(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/DLC/");
                for (int i = 0; i < levels.Length; i++)
                {
                    string dirName = Path.GetFileName(levels[i]);
                    if (dirName == "BSPNostromo_Ripley_PATCH" ||
                        dirName == "BSPNostromo_TwoTeams_PATCH")
                        continue;
                    levelFolders.Add("DLC/" + dirName.ToUpper());
                }
            }

            for (int i = 0; i < levelFolders.Count; i++)
            {
                //TODO: need to check if the TEXT folder overwrites the use of the text db xml. if it doesn't, we should do this logic the other way around

                bool dlcMap = levelFolders[i].Length > 4 && levelFolders[i].Substring(0, 4) == "DLC/";
                string levelName = dlcMap ? levelFolders[i].Substring(4) : levelFolders[i];

                Level level = _levels.FirstOrDefault(o => o.name == levelName);
                if (level == null)
                {
                    level = new Level();
                    level.name = levelName;
                    _levels.Add(level);
                }

                level.using_local_db = Directory.Exists(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/" + levelFolders[i] + "/TEXT/");
                level.is_dlc_map = dlcMap;

                if (level.using_local_db)
                {
                    level.text_dbs = File.ReadAllLines(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/" + levelFolders[i] + "/TEXT/text_db_list.txt").ToList();

                    string textPath = Singleton.PathToAI + "/DATA/ENV/PRODUCTION/" + (_levels[i].is_dlc_map ? "DLC/" : "") + _levels[i].name + "/TEXT";
                    if (Directory.Exists(textPath))
                    {
                        string[] textPaths = Directory.GetFiles(textPath, "*.TXT", SearchOption.AllDirectories);
                        foreach (string path in textPaths)
                        {
                            string dbName = Path.GetFileNameWithoutExtension(path);
                            if (dbName == "text_db_list")
                                continue;
                            _levels[i].available_local_dbs.Add(dbName);
                        }
                    }
                }
            }
        }

        private void WriteLevelTextDatabasesXML()
        {
            List<string> output = new List<string>();
            output.Add("<level_text_databases>");
            for (int i = 0; i < _levels.Count; i++)
            {
                if (_levels[i].using_local_db)
                    continue;

                output.Add("\t<level name=\"" + _levels[i].name + "\">");
                for (int x = 0; x < _levels[i].text_dbs.Count; x++)
                {
                    output.Add("\t\t<text_database name=\"" + _levels[i].text_dbs[x] + "\">");
                }
            }
            File.WriteAllLines(Singleton.PathToAI + "/DATA/level_text_databases.xml", output);
        }

        private void WriteLocalTextDBs()
        {
            for (int i = 0; i < _levels.Count; i++)
            {
                if (!_levels[i].using_local_db)
                    continue;

                //Manage the TEXT directory in the level
            }
        }

        class Level
        {
            public bool using_local_db = false; //If this is true, we should write to text_db_list.txt instead of the XML
            public bool is_dlc_map = false;

            public string name;
            public List<string> text_dbs = new List<string>();

            public HashSet<string> available_local_dbs = new HashSet<string>();
        }
    }
}

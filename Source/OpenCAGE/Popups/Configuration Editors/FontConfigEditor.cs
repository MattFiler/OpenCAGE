using CATHODE;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

namespace OpenCAGE.ConfigEditors
{
    /// <summary>
    /// font_config.xml - which typeface each language uses for the game's five named font slots, and the
    /// Scaleform library the faces come out of.
    ///
    /// The PC build reads DATA/FONT_CONFIG.XML (the only spelling in AI.exe; there is no font_config.bml
    /// string, unlike every config the engine really does read as BML). Switch and mobile builds ship the
    /// same data as font_config_switch, and a BML sits beside the XML in some installs, so every variant
    /// that is present is written on save and they cannot drift apart.
    /// </summary>
    public partial class FontConfigEditor : BaseWindow
    {
        //Every spelling of the font config, in the order we would rather read from
        private static readonly string[] ConfigFiles =
        {
            "FONT_CONFIG.XML",
            "FONT_CONFIG.BML",
            "FONT_CONFIG_SWITCH.XML",
            "FONT_CONFIG_SWITCH.BML",
        };

        private XmlDocument _config;
        private readonly List<string> _files = new List<string>();
        private XmlElement _selectedLanguage;
        private bool _populating;

        public FontConfigEditor() : base()
        {
            InitializeComponent();

            foreach (string file in ConfigFiles)
            {
                string path = Singleton.PathToAI + "/DATA/" + file;
                if (!File.Exists(path))
                    continue;
                _files.Add(path);
                if (_config == null)
                    _config = ReadConfig(path);
            }

            if (_config == null)
            {
                MessageBox.Show("Could not read DATA/FONT_CONFIG.XML.\n\nUse Options -> Reset Configs to restore it, or Manage Game Directories to point OpenCAGE at a valid install.",
                    "Font config missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _loadFailed = true;
                return;
            }

            ConfigEditorUtils.ShowAutoSaveTipOnce();

            languageList.BeginUpdate();
            foreach (XmlElement language in Languages())
                languageList.Items.Add(language.GetAttribute("name"));
            languageList.EndUpdate();

            if (languageList.Items.Count > 0)
                languageList.SelectedIndex = 0;

            Singleton.OnResetConfigs += OnResetConfigs;
            FormClosed += (s, e) => { Singleton.OnResetConfigs -= OnResetConfigs; };
        }

        private bool _loadFailed;
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

        private static XmlDocument ReadConfig(string path)
        {
            try
            {
                if (Path.GetExtension(path).ToUpper() == ".BML")
                {
                    BML bml = new BML(path);
                    return bml.Loaded ? bml.Content : null;
                }

                XmlDocument doc = new XmlDocument();
                //Keep the file's own indentation, so editing one attribute doesn't rewrite every line
                doc.PreserveWhitespace = true;
                doc.Load(path);
                return doc;
            }
            catch (Exception e)
            {
                Debug.Log("Font Config", "Failed to read " + path + ": " + e.Message);
                return null;
            }
        }

        private List<XmlElement> Languages()
        {
            XmlElement root = _config?["font_config"];
            if (root == null)
                return new List<XmlElement>();
            return root.ChildNodes.OfType<XmlElement>().Where(o => o.Name == "language").ToList();
        }

        private void languageList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (languageList.SelectedIndex < 0)
                return;

            List<XmlElement> languages = Languages();
            if (languageList.SelectedIndex >= languages.Count)
                return;

            _selectedLanguage = languages[languageList.SelectedIndex];

            _populating = true;
            fontLib.Text = _selectedLanguage.GetAttribute("font_lib");
            fontGrid.Rows.Clear();
            foreach (XmlElement font in _selectedLanguage.ChildNodes.OfType<XmlElement>().Where(o => o.Name == "font"))
            {
                int row = fontGrid.Rows.Add(font.GetAttribute("id"), font.GetAttribute("name"), font.GetAttribute("style"));
                fontGrid.Rows[row].Tag = font;
            }
            _populating = false;

            removeFont.Enabled = fontGrid.Rows.Count > 0;
        }

        private void fontLib_TextChanged(object sender, EventArgs e)
        {
            if (_populating || _selectedLanguage == null)
                return;
            _selectedLanguage.SetAttribute("font_lib", fontLib.Text);
            Save();
        }

        /* Commit a cell as soon as it's typed into, so CellValueChanged runs without waiting for focus to move */
        private void fontGrid_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (fontGrid.IsCurrentCellDirty)
                fontGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void fontGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_populating || e.RowIndex < 0 || e.RowIndex >= fontGrid.Rows.Count)
                return;

            DataGridViewRow row = fontGrid.Rows[e.RowIndex];
            XmlElement font = row.Tag as XmlElement;
            if (font == null)
                return;

            font.SetAttribute("id", Cell(row, 0));
            font.SetAttribute("name", Cell(row, 1));
            font.SetAttribute("style", Cell(row, 2));
            Save();
        }

        private static string Cell(DataGridViewRow row, int index)
        {
            return row.Cells[index].Value?.ToString() ?? "";
        }

        private void addFont_Click(object sender, EventArgs e)
        {
            if (_selectedLanguage == null)
                return;

            XmlElement font = _config.CreateElement("font");
            font.SetAttribute("id", "$NewFont");
            font.SetAttribute("name", "Isolation");
            font.SetAttribute("style", "bold");
            ConfigEditorUtils.AppendIndented(_selectedLanguage, font, "\r\n\t\t");

            _populating = true;
            int row = fontGrid.Rows.Add(font.GetAttribute("id"), font.GetAttribute("name"), font.GetAttribute("style"));
            fontGrid.Rows[row].Tag = font;
            _populating = false;

            fontGrid.CurrentCell = fontGrid.Rows[row].Cells[0];
            removeFont.Enabled = true;
            Save();
        }

        private void removeFont_Click(object sender, EventArgs e)
        {
            if (_selectedLanguage == null || fontGrid.CurrentRow == null)
                return;

            XmlElement font = fontGrid.CurrentRow.Tag as XmlElement;
            if (font?.ParentNode != null)
            {
                //Take the indentation in front of it too, or the line it was on is left blank
                if (font.PreviousSibling != null && font.PreviousSibling.NodeType == XmlNodeType.Whitespace)
                    font.ParentNode.RemoveChild(font.PreviousSibling);
                font.ParentNode.RemoveChild(font);
            }

            _populating = true;
            fontGrid.Rows.Remove(fontGrid.CurrentRow);
            _populating = false;

            removeFont.Enabled = fontGrid.Rows.Count > 0;
            Save();
        }

        private void Save()
        {
            try
            {
                foreach (string path in _files)
                {
                    if (Path.GetExtension(path).ToUpper() == ".BML")
                    {
                        BML bml = new BML(path);
                        bml.Content = _config;
                        if (!bml.Save())
                            throw new IOException("Could not write " + Path.GetFileName(path));
                    }
                    else
                    {
                        Modding.ModServices.CaptureBeforeWrite(path);
                        _config.Save(path);
                    }
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
    }
}

using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.ConfigEditors
{
    public partial class LocalisationEditor : BaseWindow
    {

        // todo - when i implement this i need to add it to the backup tool (?)

        readonly LocalisationHandler _localisation = new LocalisationHandler();
        readonly TextBox[] _languageTextBoxes;
        List<LocalisedText> _allEnglishStrings = new List<LocalisedText>();
        string _currentTextId;
        string _currentMissionId;

        public LocalisationEditor() : base()
        {
            InitializeComponent();
            _languageTextBoxes = new[]
            {
                textLangCzech,
                textLangEnglish,
                textLangFrench,
                textLangGerman,
                textLangItalian,
                textLangPolish,
                textLangPortuguese,
                textLangRussian,
                textLangSpanish
            };
            LayoutLanguagePanelWidths();
            LayoutSearchBar();
        }

        private void LocalisationEditor_Load(object sender, EventArgs e)
        {
            LayoutSearchBar();
            PopulateStringList();
        }

        private void panelLanguagesScroll_Resize(object sender, EventArgs e)
        {
            LayoutLanguagePanelWidths();
        }

        private void panelListSearch_Resize(object sender, EventArgs e)
        {
            LayoutSearchBar();
        }

        private void LayoutSearchBar()
        {
            if (labelSearch == null || textSearchFilter == null || panelListSearch == null)
                return;
            int x = labelSearch.Right + 8;
            textSearchFilter.Left = x;
            textSearchFilter.Width = Math.Max(50, panelListSearch.ClientSize.Width - x - 8);
        }

        private void LayoutLanguagePanelWidths()
        {
            int w = Math.Max(100, panelLanguagesScroll.ClientSize.Width - 24);
            tableLayoutLanguages.Width = w;
        }

        private void textSearchFilter_TextChanged(object sender, EventArgs e)
        {
            searchDebounceTimer.Stop();
            searchDebounceTimer.Start();
        }

        private void searchDebounceTimer_Tick(object sender, EventArgs e)
        {
            searchDebounceTimer.Stop();
            ApplySearchFilter();
        }

        private void PopulateStringList()
        {
            _allEnglishStrings = _localisation.GetAllStringsWithValues(LocalisationHandler.AYZ_Lang.ENGLISH);
            ApplySearchFilter();
        }

        private void ApplySearchFilter()
        {
            string needle = (textSearchFilter.Text ?? string.Empty).Trim();
            List<LocalisedText> visible;
            if (needle.Length == 0)
            {
                visible = _allEnglishStrings;
            }
            else
            {
                visible = new List<LocalisedText>();
                foreach (LocalisedText t in _allEnglishStrings)
                {
                    if (t.TextID.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        visible.Add(t);
                        continue;
                    }
                    string english = t.TextValue ?? string.Empty;
                    if (english.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                        visible.Add(t);
                }
            }

            string keepTextId = _currentTextId;
            string keepMissionId = _currentMissionId;

            RebuildListView(visible);

            if (string.IsNullOrEmpty(keepTextId))
                return;

            foreach (ListViewItem it in listViewStrings.Items)
            {
                var tag = (Tuple<string, string>)it.Tag;
                if (tag.Item1 == keepTextId && tag.Item2 == keepMissionId)
                {
                    it.Selected = true;
                    it.Focused = true;
                    it.EnsureVisible();
                    return;
                }
            }

            ClearLanguageEditors();
        }

        private void RebuildListView(IList<LocalisedText> strings)
        {
            listViewStrings.BeginUpdate();
            try
            {
                listViewStrings.Items.Clear();
                listViewStrings.Groups.Clear();

                var seenMissions = new HashSet<string>();
                var missionOrder = new List<string>();
                foreach (LocalisedText t in strings)
                {
                    if (seenMissions.Add(t.MissionID))
                        missionOrder.Add(t.MissionID);
                }

                var groupMap = new Dictionary<string, ListViewGroup>(StringComparer.Ordinal);
                foreach (string mission in missionOrder)
                {
                    var g = new ListViewGroup(mission, HorizontalAlignment.Left);
                    listViewStrings.Groups.Add(g);
                    groupMap[mission] = g;
                }

                var items = new ListViewItem[strings.Count];
                for (int idx = 0; idx < strings.Count; idx++)
                {
                    LocalisedText t = strings[idx];
                    var item = new ListViewItem(t.TextID) { Group = groupMap[t.MissionID] };
                    item.SubItems.Add(string.IsNullOrEmpty(t.TextValue) ? "—" : t.TextValue);
                    item.Tag = Tuple.Create(t.TextID, t.MissionID);
                    items[idx] = item;
                }

                listViewStrings.Items.AddRange(items);
            }
            finally
            {
                listViewStrings.EndUpdate();
            }
        }

        private void listViewStrings_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listViewStrings.SelectedItems.Count == 0)
            {
                ClearLanguageEditors();
                return;
            }

            ListViewItem item = listViewStrings.SelectedItems[0];
            var tag = (Tuple<string, string>)item.Tag;
            _currentTextId = tag.Item1;
            _currentMissionId = tag.Item2;

            for (int i = 0; i < _languageTextBoxes.Length; i++)
            {
                try
                {
                    LocalisedText lt = _localisation.GetLocalisedString(_currentTextId, (LocalisationHandler.AYZ_Lang)i);
                    _languageTextBoxes[i].Text = lt.TextValue;
                }
                catch (InvalidOperationException)
                {
                    _languageTextBoxes[i].Text = "";
                }
            }
        }

        private void ClearLanguageEditors()
        {
            _currentTextId = null;
            _currentMissionId = null;
            foreach (TextBox tb in _languageTextBoxes)
                tb.Clear();
        }

        private void buttonAddString_Click(object sender, EventArgs e)
        {
            using (var dlg = new AddLocalisedStringDialog(_localisation))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                string err;
                if (!_localisation.TryAppendNewString(dlg.SelectedMissionId, dlg.NewTextId, dlg.TextsPerLanguage, out err))
                {
                    MessageBox.Show(this, err, "Could not add string", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                PopulateStringList();
                SelectStringInList(dlg.NewTextId, dlg.SelectedMissionId);
                MessageBox.Show(this, "String added to all language files.", "Added", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void SelectStringInList(string textId, string missionId)
        {
            foreach (ListViewItem it in listViewStrings.Items)
            {
                var tag = (Tuple<string, string>)it.Tag;
                if (tag.Item1 == textId && tag.Item2 == missionId)
                {
                    it.Selected = true;
                    it.Focused = true;
                    it.EnsureVisible();
                    listViewStrings.Focus();
                    return;
                }
            }
        }

        private void updateString_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTextId) || string.IsNullOrEmpty(_currentMissionId))
            {
                MessageBox.Show("Select a string from the list before saving.", "No string selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            for (int i = 0; i < _languageTextBoxes.Length; i++)
            {
                var lt = new LocalisedText(
                    _languageTextBoxes[i].Text,
                    _currentTextId,
                    _currentMissionId,
                    _localisation.languageFolders[i]);
                if (!_localisation.UpdateLocalisedString(lt))
                {
                    MessageBox.Show("An unknown error occurred while saving " + _localisation.languageFolders[i] + ".", "Couldn't save text", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            int englishIndex = (int)LocalisationHandler.AYZ_Lang.ENGLISH;
            string newEnglish = englishIndex < _languageTextBoxes.Length ? _languageTextBoxes[englishIndex].Text : null;
            for (int i = 0; i < _allEnglishStrings.Count; i++)
            {
                if (_allEnglishStrings[i].TextID == _currentTextId && _allEnglishStrings[i].MissionID == _currentMissionId)
                {
                    LocalisedText updated = _allEnglishStrings[i];
                    updated.TextValue = newEnglish;
                    _allEnglishStrings[i] = updated;
                    break;
                }
            }

            if (listViewStrings.SelectedItems.Count > 0)
            {
                ListViewItem item = listViewStrings.SelectedItems[0];
                if (englishIndex < _languageTextBoxes.Length)
                    item.SubItems[1].Text = string.IsNullOrEmpty(_languageTextBoxes[englishIndex].Text) ? "—" : _languageTextBoxes[englishIndex].Text;
            }

            //TODO: need to refresh the loaded text DBs and loaded enum string controls!

            MessageBox.Show("Saved all languages for this string.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

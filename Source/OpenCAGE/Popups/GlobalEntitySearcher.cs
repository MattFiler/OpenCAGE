using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups;
using OpenCAGE.Popups.Base;
using OpenCAGE.Scripts;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class GlobalEntitySearcher : BaseWindow
    {
        public Action<Composite, Entity> OnEntitySelected;

        private Dictionary<Entity, Composite> _entityComposites = new Dictionary<Entity, Composite>();
        private string _baseText;

        private SearchMode _searchMode;
        public enum SearchMode
        {
            BY_NAME,
            BY_FUNCTION,
            BY_COMPOSITE,
        }

        public GlobalEntitySearcher(SearchMode mode, Composite composite = null) : base(composite == null ? WindowClosesOn.COMMANDS_RELOAD : WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();

            this.FormClosing += ShowCompositeUses_FormClosing;

            _searchMode = mode;

            nameSearchBox.Visible = false;
            entityVariant.Visible = false;
            searchFunctionTypes.Visible = false;

            GlobalEntitySearchHelper.SetupEntityListColumns(entityList, SettingsManager.GetBool(Settings.ShowShortGuids));

            switch (mode)
            {
                case SearchMode.BY_FUNCTION:
                    {
                        _baseText = "Function Uses";
                        entityVariant.Visible = true;
                        searchFunctionTypes.Visible = true;
                        List<string> functionsOrdered = new List<string>();
                        foreach (FunctionType function in Enum.GetValues(typeof(FunctionType)))
                            functionsOrdered.Add(function.ToString());
                        functionsOrdered.Sort();
                        foreach (string function in functionsOrdered)
                            entityVariant.Items.Add(function);
                        entityVariant.SelectedIndex = SettingsManager.GetInteger(Settings.PrevFuncUsesSearch);
                    }
                    break;
                case SearchMode.BY_NAME:
                    {
                        nameSearchBox.Visible = true;
                        searchFunctionTypes.Visible = true;
                        searchFunctionTypes.Text = "Search";
                        this.Text = "Search for entity by name";
                        nameSearchBox.Text = SettingsManager.GetString(Settings.PrevEntNameSearch);
                        if (nameSearchBox.Text != "")
                            Search(nameSearchBox.Text);
                    }
                    break;
                case SearchMode.BY_COMPOSITE:
                    {
                        _baseText = "Composite Uses";
                        label.Text = "Entities that instance the composite '" + composite.name + "':";
                        SearchByComposite(composite.shortGUID);
                    }
                    break;
            }
        }

        private void GlobalEntitySearcher_Load(object sender, EventArgs e)
        {
            nameSearchBox.Select();
        }

        private void ShowCompositeUses_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_funcSelector != null)
            {
                _funcSelector.OnTypeSelected -= OnFunctionTypeSelected;
                _funcSelector.Close();
            }
        }

        private void CreateEntityOnEnterKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                searchFunctionTypes.PerformClick();
        }

        private void jumpToEntity_Click(object sender, EventArgs e)
        {
            if (entityList.SelectedItems.Count == 0)
                return;

            Entity selected = (Entity)entityList.SelectedItems[0].Tag;
            if (OnEntitySelected != null)
                OnEntitySelected.Invoke(_entityComposites[selected], selected);
            else
                GlobalEntitySearchHelper.JumpToSelectedEntity(entityList, _entityComposites);

            if (!SettingsManager.GetBool(Settings.KeepUsesWindowOpen))
                this.Close();
        }

        private void entityVariant_SelectedIndexChanged(object sender, EventArgs e)
        {
            SettingsManager.SetInteger(Settings.PrevFuncUsesSearch, entityVariant.SelectedIndex);
            Search(new ShortGuid((uint)Enum.Parse(typeof(FunctionType), entityVariant.Text)));
        }

        private void Search(ShortGuid functionGuid)
        {
            if (_searchMode == SearchMode.BY_NAME)
                throw new Exception("");

            int count = GlobalEntitySearchHelper.SearchByFunction(Content, functionGuid, entityList, _entityComposites);
            Text = _baseText + " - " + (entityVariant.Text != "" ? entityVariant.Text + " " : "") + "(" + count + ")";
        }

        private void Search(string name)
        {
            if (_searchMode != SearchMode.BY_NAME)
                throw new Exception("");

            if (name == "")
            {
                MessageBox.Show("Please enter a search term!", "Empty search", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SettingsManager.SetString(Settings.PrevEntNameSearch, name);
            int count = GlobalEntitySearchHelper.SearchByName(Content, name, entityList, _entityComposites);
            Text = "Results for '" + name + "' (" + count + ")";
        }

        SelectFunctionType _funcSelector = null;
        private void searchFunctionTypes_Click(object sender, EventArgs e)
        {
            switch (_searchMode)
            {
                case SearchMode.BY_NAME:
                    Search(nameSearchBox.Text);
                    break;

                case SearchMode.BY_FUNCTION:
                    if (_funcSelector != null)
                    {
                        _funcSelector.OnTypeSelected -= OnFunctionTypeSelected;
                        _funcSelector.Close();
                    }
                    _funcSelector = new SelectFunctionType();
                    _funcSelector.OnTypeSelected += OnFunctionTypeSelected;
                    _funcSelector.Show();
                    break;
            }
        }
        private void OnFunctionTypeSelected(FunctionType type)
        {
            entityVariant.SelectedItem = type.ToString();
        }

        private void SearchByComposite(ShortGuid compositeGuid)
        {
            if (_searchMode != SearchMode.BY_COMPOSITE)
                throw new Exception("");

            _entityComposites.Clear();

            entityList.BeginUpdate();
            entityList.Items.Clear();
            entityList.Groups.Clear();
            foreach (Composite comp in Content.Level.Commands.Entries)
            {
                List<FunctionEntity> funcs = comp.functions.FindAll(o => o.function == compositeGuid);
                if (funcs.Count == 0)
                    continue;
                entityList.Groups.Add(new ListViewGroup() { Header = comp.name });
                foreach (FunctionEntity ent in funcs)
                {
                    ListViewItem item = (ListViewItem)Content.GenerateListViewItem(ent, comp).Clone();
                    item.Group = entityList.Groups[entityList.Groups.Count - 1];
                    item.ImageIndex = ent.function.IsFunctionType ? 1 : 2;
                    entityList.Items.Add(item);
                    _entityComposites.Add(ent, comp);
                }
            }
            entityList.EndUpdate();

            Text = _baseText + " (" + entityList.Items.Count + ")";
        }
    }
}

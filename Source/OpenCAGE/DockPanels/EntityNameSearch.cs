using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.Scripts;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.DockPanels
{
    public partial class EntityNameSearch : DockContent
    {
        private readonly Dictionary<Entity, Composite> _entityComposites = new Dictionary<Entity, Composite>();
        private string _currentSearch = "";

        protected LevelContent Content => Singleton.Editor?.CompositeBrowser?.Content;

        public EntityNameSearch()
        {
            InitializeComponent();

            CloseButtonVisible = false;
            AllowEndUserDocking = false;
            FormClosing += EntityNameSearch_FormClosing;

            entityList.DoubleClick += (s, e) => JumpToSelectedEntity();
            nameSearchBox.KeyDown += NameSearchBox_KeyDown;
            clearSearchBtn.Click += ClearSearchBtn_Click;

            GlobalEntitySearchScopeSettings.BindSettingsButton(scopeSettingsBtn);
            GlobalEntitySearchScopeSettings.AddScopeChangedHandler(OnSearchScopeChanged);

            Singleton.OnLevelLoaded += OnLevelLoaded;
            Singleton.OnEntityDeleted += OnEntityDeleted;
            Singleton.OnCompositeSelected += OnCompositeSelected;
        }

        protected override string GetPersistString()
        {
            return "EntityNameSearch";
        }

        private void EntityNameSearch_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            GlobalEntitySearchScopeSettings.RemoveScopeChangedHandler(OnSearchScopeChanged);
            Singleton.OnEntityDeleted -= OnEntityDeleted;
            Singleton.OnCompositeSelected -= OnCompositeSelected;

            base.OnFormClosed(e);
        }

        private void OnCompositeSelected(Composite composite)
        {
            if (IsDisposed
                || string.IsNullOrWhiteSpace(_currentSearch)
                || Content == null
                || !GlobalEntitySearchHelper.ShouldRefreshSearchForCompositeChange(GlobalEntitySearchScopeSettings.Scope))
            {
                return;
            }

            BeginInvoke(new Action(RunSearch));
        }

        private void OnEntityDeleted(Entity entity)
        {
            if (IsDisposed || entity == null || entityList.Items.Count == 0)
                return;

            if (!GlobalEntitySearchHelper.RemoveDeletedEntityFromResults(entity, entityList, _entityComposites))
                return;

            if (!string.IsNullOrWhiteSpace(_currentSearch))
                Text = "Search by Name - '" + _currentSearch + "' (" + entityList.Items.Count + ")";
        }

        private void OnLevelLoaded(LevelContent content)
        {
            if (IsDisposed)
                return;

            BeginInvoke(new Action(InitializeFromLevel));
        }

        public void InitializeFromLevel()
        {
            GlobalEntitySearchHelper.SetupEntityListColumns(entityList, SettingsManager.GetBool(Settings.ShowShortGuids));
            nameSearchBox.Text = SettingsManager.GetString(Settings.PrevEntNameSearch);
            _currentSearch = nameSearchBox.Text;
            clearSearchBtn.Visible = _currentSearch != "";

            if (!string.IsNullOrWhiteSpace(nameSearchBox.Text))
                RunSearch();
            else
                ClearResults();
        }

        public void FocusSearch()
        {
            if (Pane != null && DockState != DockState.Hidden && DockState != DockState.Float)
                Activate();
            else
                Show(Singleton.Editor.DockPanel, DockState.DockLeft);

            nameSearchBox.Focus();
            nameSearchBox.SelectAll();
        }

        private void NameSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                RunSearch();
                e.SuppressKeyPress = true;
            }
        }

        private void ClearSearchBtn_Click(object sender, EventArgs e)
        {
            if (nameSearchBox.Text == "" && _currentSearch == "")
                return;

            nameSearchBox.Text = "";
            _currentSearch = "";
            clearSearchBtn.Visible = false;
            SettingsManager.SetString(Settings.PrevEntNameSearch, "");
            ClearResults();
        }

        private void OnSearchScopeChanged()
        {
            if (IsDisposed || string.IsNullOrWhiteSpace(_currentSearch))
                return;

            BeginInvoke(new Action(RunSearch));
        }

        private void RunSearch()
        {
            if (Content == null)
                return;

            string name = nameSearchBox.Text;
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please enter a search term!", "Empty search", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _currentSearch = name;
            clearSearchBtn.Visible = true;
            SettingsManager.SetString(Settings.PrevEntNameSearch, name);
            GlobalEntitySearchHelper.SetupEntityListColumns(entityList, SettingsManager.GetBool(Settings.ShowShortGuids));
            int count = GlobalEntitySearchHelper.SearchByName(Content, name, entityList, _entityComposites);
            Text = "Search by Name - '" + name + "' (" + count + ")";
        }

        private void ClearResults()
        {
            _entityComposites.Clear();
            entityList.Items.Clear();
            entityList.Groups.Clear();
            Text = "Search by Name";
        }

        private void JumpToSelectedEntity()
        {
            GlobalEntitySearchHelper.JumpToSelectedEntity(entityList, _entityComposites);
        }
    }
}

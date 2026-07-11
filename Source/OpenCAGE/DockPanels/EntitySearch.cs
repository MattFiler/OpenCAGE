using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.Popups;
using OpenCAGE.Scripts;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.DockPanels
{
    public partial class EntitySearch : DockContent
    {
        public enum SearchMode
        {
            ByName = 0,
            ByFunction = 1,
            ByComposite = 2,
        }

        private static readonly string[] ModeLabels =
        {
            "Entity By Name",
            "Function Entities",
            "Composite Instances",
        };

        private readonly Dictionary<Entity, Composite> _entityComposites = new Dictionary<Entity, Composite>();
        private SelectFunctionType _funcSelector = null;
        private SelectComposite _compositeSelector = null;
        private Composite _selectedComposite = null;
        private string _currentNameSearch = "";
        private SearchMode _currentMode = SearchMode.ByName;
        private bool _initializing = false;

        protected LevelContent Content => Singleton.Editor?.CompositeBrowser?.Content;

        public EntitySearch()
        {
            InitializeComponent();

            CloseButton = false;
            CloseButtonVisible = false;
            AllowEndUserDocking = false;
            FormClosing += EntitySearch_FormClosing;

            foreach (string label in ModeLabels)
                modeCombo.Items.Add(label);

            modeCombo.SelectedIndexChanged += ModeCombo_SelectedIndexChanged;

            entityList.DoubleClick += (s, e) => JumpToSelectedEntity();

            nameSearchBox.KeyDown += NameSearchBox_KeyDown;
            clearSearchBtn.Click += ClearSearchBtn_Click;

            functionTypeCombo.SelectedIndexChanged += FunctionTypeCombo_SelectedIndexChanged;
            browseFunctionButton.Click += BrowseFunctionButton_Click;
            GlobalEntitySearchScopeSettings.SetIconButtonImage(
                browseFunctionButton,
                OpenCAGE.Properties.Resources.d_ScriptableObject_Icon_braces_only);

            browseCompositeButton.Click += BrowseCompositeButton_Click;
            GlobalEntitySearchScopeSettings.SetIconButtonImage(
                browseCompositeButton,
                OpenCAGE.Properties.Resources.d_PrefabVariant_Icon);

            GlobalEntitySearchScopeSettings.BindSettingsButton(scopeSettingsBtn);
            GlobalEntitySearchScopeSettings.AddScopeChangedHandler(OnSearchScopeChanged);

            SettingsManager.SettingsChanged += OnSettingsChanged;

            Singleton.OnLevelLoaded += OnLevelLoaded;
            Singleton.OnEntityDeleted += OnEntityDeleted;
            Singleton.OnCompositeSelected += OnCompositeSelected;
        }

        protected override string GetPersistString()
        {
            return "EntitySearch";
        }

        private void EntitySearch_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            CloseFunctionTypeSelector();
            CloseCompositeSelector();
            Hide();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SettingsManager.SettingsChanged -= OnSettingsChanged;
            GlobalEntitySearchScopeSettings.RemoveScopeChangedHandler(OnSearchScopeChanged);
            Singleton.OnEntityDeleted -= OnEntityDeleted;
            Singleton.OnCompositeSelected -= OnCompositeSelected;

            base.OnFormClosed(e);
        }

        public void InitializeFromLevel()
        {
            _initializing = true;

            GlobalEntitySearchHelper.SetupEntityListColumns(entityList, SettingsManager.GetBool(Settings.ShowShortGuids));

            PopulateFunctionTypes();
            RestoreSelectedComposite();

            _currentNameSearch = SettingsManager.GetString(Settings.PrevEntNameSearch);
            nameSearchBox.Text = _currentNameSearch;

            _currentMode = (SearchMode)SettingsManager.GetInteger(Settings.EntitySearchMode, (int)SearchMode.ByName);
            if (modeCombo.SelectedIndex != (int)_currentMode)
                modeCombo.SelectedIndex = (int)_currentMode;

            _initializing = false;

            ApplyMode(_currentMode, runSearch: true);
        }

        private void OnSettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            if (!e.ExternalChange || e.ChangedKeys.Count == 0 || IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnSettingsChanged(sender, e)));
                return;
            }

            foreach (string key in e.ChangedKeys)
            {
                switch (key)
                {
                    case Settings.ShowShortGuids:
                        GlobalEntitySearchHelper.SetupEntityListColumns(entityList, SettingsManager.GetBool(Settings.ShowShortGuids));
                        break;
                    case Settings.EntitySearchMode:
                        {
                            SearchMode mode = (SearchMode)SettingsManager.GetInteger(Settings.EntitySearchMode, (int)SearchMode.ByName);
                            if (modeCombo.SelectedIndex != (int)mode)
                            {
                                _initializing = true;
                                modeCombo.SelectedIndex = (int)mode;
                                _initializing = false;
                                ApplyMode(mode, runSearch: true);
                            }
                        }
                        break;
                }
            }
        }

        private void PopulateFunctionTypes()
        {
            functionTypeCombo.BeginUpdate();
            functionTypeCombo.Items.Clear();
            List<string> functionsOrdered = new List<string>();
            foreach (FunctionType function in Enum.GetValues(typeof(FunctionType)))
                functionsOrdered.Add(function.ToString());
            functionsOrdered.Sort();
            foreach (string function in functionsOrdered)
                functionTypeCombo.Items.Add(function);
            functionTypeCombo.EndUpdate();

            int savedIndex = SettingsManager.GetInteger(Settings.PrevFuncUsesSearch);
            if (savedIndex >= 0 && savedIndex < functionTypeCombo.Items.Count)
                functionTypeCombo.SelectedIndex = savedIndex;
            else if (functionTypeCombo.Items.Count > 0)
                functionTypeCombo.SelectedIndex = 0;
        }

        private void RestoreSelectedComposite()
        {
            _selectedComposite = null;
            if (Content?.Level?.Commands == null)
            {
                compositeNameBox.Text = "";
                return;
            }

            string savedName = SettingsManager.GetString(Settings.PrevCompositeUsesSearch);
            if (!string.IsNullOrEmpty(savedName))
                _selectedComposite = Content.Level.Commands.GetComposite(savedName);

            if (_selectedComposite == null)
                _selectedComposite = GetDefaultComposite();

            compositeNameBox.Text = _selectedComposite?.name ?? "";
        }

        private Composite GetDefaultComposite()
        {
            if (Content?.Level?.Commands == null)
                return null;

            Composite current = Singleton.Editor?.CompositeDisplay?.Populated == true
                ? Singleton.Editor.CompositeDisplay.Composite
                : null;
            if (current != null)
                return current;

            Composite[] entryPoints = Content.Level.Commands.EntryPoints;
            if (entryPoints != null && entryPoints.Length > 0 && entryPoints[0] != null)
                return entryPoints[0];

            return Content.Level.Commands.Entries.Count > 0 ? Content.Level.Commands.Entries[0] : null;
        }

        public void FocusSearch()
        {
            if (Pane != null && DockState != DockState.Hidden && DockState != DockState.Float)
                Activate();
            else
                Show(Singleton.Editor.DockPanel, DockState.DockLeft);

            switch (_currentMode)
            {
                case SearchMode.ByName:
                    nameSearchBox.Focus();
                    nameSearchBox.SelectAll();
                    break;
                case SearchMode.ByFunction:
                    functionTypeCombo.Focus();
                    break;
                case SearchMode.ByComposite:
                    browseCompositeButton.Focus();
                    break;
            }
        }

        public void FocusSearch(SearchMode mode)
        {
            SetMode(mode);
            FocusSearch();
        }

        public void SearchForComposite(Composite composite)
        {
            if (composite == null)
                return;

            SetMode(SearchMode.ByComposite);
            SetSelectedComposite(composite);
            FocusSearch();
        }

        private void SetMode(SearchMode mode)
        {
            if (modeCombo.SelectedIndex == (int)mode)
            {
                ApplyMode(mode, runSearch: true);
                return;
            }

            modeCombo.SelectedIndex = (int)mode;
        }

        private void ModeCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_initializing || modeCombo.SelectedIndex < 0)
                return;

            SearchMode mode = (SearchMode)modeCombo.SelectedIndex;
            SettingsManager.SetInteger(Settings.EntitySearchMode, (int)mode);
            ApplyMode(mode, runSearch: true);
        }

        private void ApplyMode(SearchMode mode, bool runSearch)
        {
            _currentMode = mode;

            nameSearchBox.Visible = mode == SearchMode.ByName;
            clearSearchBtn.Visible = mode == SearchMode.ByName && !string.IsNullOrEmpty(_currentNameSearch);
            functionTypeCombo.Visible = mode == SearchMode.ByFunction;
            browseFunctionButton.Visible = mode == SearchMode.ByFunction;
            compositeNameBox.Visible = mode == SearchMode.ByComposite;
            browseCompositeButton.Visible = mode == SearchMode.ByComposite;

            if (runSearch)
                RunSearch();
        }

        private void OnLevelLoaded(LevelContent content)
        {
            if (IsDisposed)
                return;

            BeginInvoke(new Action(InitializeFromLevel));
        }

        private void OnCompositeSelected(Composite composite)
        {
            if (IsDisposed
                || Content == null
                || !HasActiveQuery()
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

            UpdateResultTitle(entityList.Items.Count);
        }

        private void OnSearchScopeChanged()
        {
            if (IsDisposed || Content == null || !HasActiveQuery())
                return;

            BeginInvoke(new Action(RunSearch));
        }

        private bool HasActiveQuery()
        {
            switch (_currentMode)
            {
                case SearchMode.ByName:
                    return !string.IsNullOrWhiteSpace(_currentNameSearch);
                case SearchMode.ByFunction:
                    return functionTypeCombo.SelectedIndex >= 0;
                case SearchMode.ByComposite:
                    return _selectedComposite != null;
                default:
                    return false;
            }
        }

        private void NameSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                RunNameSearch();
                e.SuppressKeyPress = true;
            }
        }

        private void ClearSearchBtn_Click(object sender, EventArgs e)
        {
            if (nameSearchBox.Text == "" && _currentNameSearch == "")
                return;

            nameSearchBox.Text = "";
            _currentNameSearch = "";
            clearSearchBtn.Visible = false;
            SettingsManager.SetString(Settings.PrevEntNameSearch, "");
            ClearResults();
        }

        private void FunctionTypeCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_initializing || _currentMode != SearchMode.ByFunction || functionTypeCombo.SelectedIndex < 0 || Content == null)
                return;

            SettingsManager.SetInteger(Settings.PrevFuncUsesSearch, functionTypeCombo.SelectedIndex);
            RunFunctionSearch();
        }

        private void BrowseFunctionButton_Click(object sender, EventArgs e)
        {
            CloseFunctionTypeSelector();
            _funcSelector = new SelectFunctionType();
            _funcSelector.OnTypeSelected += OnFunctionTypeSelected;
            _funcSelector.Show();
        }

        private void OnFunctionTypeSelected(FunctionType type)
        {
            functionTypeCombo.SelectedItem = type.ToString();
        }

        private void CloseFunctionTypeSelector()
        {
            if (_funcSelector == null)
                return;

            _funcSelector.OnTypeSelected -= OnFunctionTypeSelected;
            _funcSelector.Close();
            _funcSelector = null;
        }

        private void BrowseCompositeButton_Click(object sender, EventArgs e)
        {
            if (Content?.Level?.Commands == null)
                return;

            CloseCompositeSelector();
            _compositeSelector = new SelectComposite(_selectedComposite?.name);
            _compositeSelector.OnCompositeGenerated += OnCompositePicked;
            _compositeSelector.Show();
        }

        private void OnCompositePicked(Composite composite)
        {
            SetSelectedComposite(composite);
        }

        private void SetSelectedComposite(Composite composite)
        {
            if (composite == null)
                return;

            _selectedComposite = composite;
            compositeNameBox.Text = composite.name;
            SettingsManager.SetString(Settings.PrevCompositeUsesSearch, composite.name);

            if (_currentMode == SearchMode.ByComposite)
                RunCompositeSearch();
        }

        private void CloseCompositeSelector()
        {
            if (_compositeSelector == null)
                return;

            _compositeSelector.OnCompositeGenerated -= OnCompositePicked;
            _compositeSelector.Close();
            _compositeSelector = null;
        }

        private void RunSearch()
        {
            switch (_currentMode)
            {
                case SearchMode.ByName:
                    RunNameSearch(allowEmptyClear: true);
                    break;
                case SearchMode.ByFunction:
                    RunFunctionSearch();
                    break;
                case SearchMode.ByComposite:
                    RunCompositeSearch();
                    break;
            }
        }

        private void RunNameSearch(bool allowEmptyClear = false)
        {
            if (Content == null)
                return;

            string name = nameSearchBox.Text;
            if (string.IsNullOrWhiteSpace(name))
            {
                if (allowEmptyClear)
                {
                    ClearResults();
                    return;
                }

                MessageBox.Show("Please enter a search term!", "Empty search", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _currentNameSearch = name;
            clearSearchBtn.Visible = _currentMode == SearchMode.ByName;
            SettingsManager.SetString(Settings.PrevEntNameSearch, name);
            GlobalEntitySearchHelper.SetupEntityListColumns(entityList, SettingsManager.GetBool(Settings.ShowShortGuids));
            int count = GlobalEntitySearchHelper.SearchByName(Content, name, entityList, _entityComposites);
            UpdateResultTitle(count);
        }

        private void RunFunctionSearch()
        {
            if (Content == null || functionTypeCombo.SelectedIndex < 0)
            {
                ClearResults();
                return;
            }

            FunctionType functionType = (FunctionType)Enum.Parse(typeof(FunctionType), functionTypeCombo.Text);
            GlobalEntitySearchHelper.SetupEntityListColumns(entityList, SettingsManager.GetBool(Settings.ShowShortGuids));
            int count = GlobalEntitySearchHelper.SearchByFunction(Content, functionType, entityList, _entityComposites);
            UpdateResultTitle(count);
        }

        private void RunCompositeSearch()
        {
            if (Content == null || _selectedComposite == null)
            {
                ClearResults();
                return;
            }

            GlobalEntitySearchHelper.SetupEntityListColumns(entityList, SettingsManager.GetBool(Settings.ShowShortGuids));
            int count = GlobalEntitySearchHelper.SearchByComposite(Content, _selectedComposite.shortGUID, entityList, _entityComposites);
            UpdateResultTitle(count);
        }

        private void UpdateResultTitle(int count)
        {
            string scopeSuffix = " [In " + GlobalEntitySearchScopeSettings.Scope.ToDisplayName() + "]";
            switch (_currentMode)
            {
                case SearchMode.ByName:
                    if (!string.IsNullOrWhiteSpace(_currentNameSearch))
                        Text = "Search: '" + _currentNameSearch + "' (" + count + ")" + scopeSuffix;
                    break;
                case SearchMode.ByFunction:
                    if (functionTypeCombo.SelectedIndex >= 0)
                        Text = "Search: " + functionTypeCombo.Text + " (" + count + ")" + scopeSuffix;
                    break;
                case SearchMode.ByComposite:
                    if (_selectedComposite != null)
                        Text = "Search: " + _selectedComposite.name + " (" + count + ")" + scopeSuffix;
                    break;
            }
        }

        private void ClearResults()
        {
            _entityComposites.Clear();
            entityList.Items.Clear();
            entityList.Groups.Clear();
            Text = "Search";
        }

        private void JumpToSelectedEntity()
        {
            GlobalEntitySearchHelper.JumpToSelectedEntity(entityList, _entityComposites);
        }
    }
}

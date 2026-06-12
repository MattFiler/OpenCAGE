using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.Popups;
using OpenCAGE.Scripts;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.DockPanels
{
    public partial class FunctionTypeSearch : DockContent
    {
        private readonly Dictionary<Entity, Composite> _entityComposites = new Dictionary<Entity, Composite>();
        private SelectFunctionType _funcSelector = null;

        protected LevelContent Content => Singleton.Editor?.CompositeBrowser?.Content;

        public FunctionTypeSearch()
        {
            InitializeComponent();

            CloseButtonVisible = false;
            AllowEndUserDocking = false;
            FormClosing += FunctionTypeSearch_FormClosing;

            entityList.DoubleClick += (s, e) => JumpToSelectedEntity();
            searchTypesButton.Click += SearchTypesButton_Click;
            GlobalEntitySearchScopeSettings.SetIconButtonImage(
                searchTypesButton,
                OpenCAGE.Properties.Resources.d_ScriptableObject_Icon_braces_only);
            functionTypeCombo.SelectedIndexChanged += FunctionTypeCombo_SelectedIndexChanged;

            GlobalEntitySearchScopeSettings.BindSettingsButton(scopeSettingsBtn);
            GlobalEntitySearchScopeSettings.AddScopeChangedHandler(OnSearchScopeChanged);

            Singleton.OnLevelLoaded += OnLevelLoaded;
            Singleton.OnEntityDeleted += OnEntityDeleted;
            Singleton.OnCompositeSelected += OnCompositeSelected;
        }

        protected override string GetPersistString()
        {
            return "FunctionTypeSearch";
        }

        private void FunctionTypeSearch_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            CloseFunctionTypeSelector();
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
                || functionTypeCombo.SelectedIndex < 0
                || Content == null
                || !GlobalEntitySearchHelper.ShouldRefreshSearchForCompositeChange(GlobalEntitySearchScopeSettings.Scope))
            {
                return;
            }

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || functionTypeCombo.SelectedIndex < 0)
                    return;

                RunSearch((FunctionType)Enum.Parse(typeof(FunctionType), functionTypeCombo.Text));
            }));
        }

        private void OnEntityDeleted(Entity entity)
        {
            if (IsDisposed || entity == null || entityList.Items.Count == 0)
                return;

            if (!GlobalEntitySearchHelper.RemoveDeletedEntityFromResults(entity, entityList, _entityComposites))
                return;

            if (functionTypeCombo.SelectedIndex >= 0)
            {
                FunctionType functionType = (FunctionType)Enum.Parse(typeof(FunctionType), functionTypeCombo.Text);
                Text = "Search by Function Type - " + functionType + " (" + entityList.Items.Count + ")";
            }
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
            else
                ClearResults();
        }

        public void FocusSearch()
        {
            if (Pane != null && DockState != DockState.Hidden && DockState != DockState.Float)
                Activate();
            else
                Show(Singleton.Editor.DockPanel, DockState.DockLeft);

            functionTypeCombo.Focus();
        }

        private void FunctionTypeCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (functionTypeCombo.SelectedIndex < 0 || Content == null)
                return;

            SettingsManager.SetInteger(Settings.PrevFuncUsesSearch, functionTypeCombo.SelectedIndex);
            RunSearch((FunctionType)Enum.Parse(typeof(FunctionType), functionTypeCombo.Text));
        }

        private void SearchTypesButton_Click(object sender, EventArgs e)
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

        private void OnSearchScopeChanged()
        {
            if (IsDisposed || functionTypeCombo.SelectedIndex < 0 || Content == null)
                return;

            BeginInvoke(new Action(() =>
            {
                RunSearch((FunctionType)Enum.Parse(typeof(FunctionType), functionTypeCombo.Text));
            }));
        }

        private void RunSearch(FunctionType functionType)
        {
            GlobalEntitySearchHelper.SetupEntityListColumns(entityList, SettingsManager.GetBool(Settings.ShowShortGuids));
            int count = GlobalEntitySearchHelper.SearchByFunction(Content, functionType, entityList, _entityComposites);
            Text = "Search by Function Type - " + functionType + " (" + count + ")";
        }

        private void ClearResults()
        {
            _entityComposites.Clear();
            entityList.Items.Clear();
            entityList.Groups.Clear();
            Text = "Search by Function Type";
        }

        private void JumpToSelectedEntity()
        {
            GlobalEntitySearchHelper.JumpToSelectedEntity(entityList, _entityComposites);
        }
    }
}

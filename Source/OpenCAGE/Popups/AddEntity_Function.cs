using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class AddEntity_Function : BaseWindow
    {
        private List<ListViewItem> _items = new List<ListViewItem>();
        private Composite _composite;

        private ListViewColumnSorter _sorter = new ListViewColumnSorter();

        public AddEntity_Function(Composite composite, bool flowgraphMode) : base (WindowClosesOn.NEW_COMPOSITE_SELECTION | WindowClosesOn.COMMANDS_RELOAD)
        {
            InitializeComponent();
            _composite = composite;

            functionTypeList1.Setup();
            functionTypeList1.SelectedItemChanged += functionTypeList_SelectedIndexChanged;

            addDefaultParams.Checked = SettingsManager.GetBool(Settings.PreviouslySearchedParamPopulation, false);

#if AUTO_POPULATE_PARAMS
            addDefaultParams.Checked = true;
            addDefaultParams.Visible = false;
#endif

            SettingsManager.SettingsChanged += OnSettingsChanged;
            FormClosed += (s, e) => SettingsManager.SettingsChanged -= OnSettingsChanged;
        }

        private void OnSettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            if (!e.ExternalChange || IsDisposed)
                return;

            if (!SettingsChangedEventArgs.ContainsKey(e.ChangedKeys, Settings.PreviouslySearchedParamPopulation))
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnSettingsChanged(sender, e)));
                return;
            }

            addDefaultParams.Checked = SettingsManager.GetBool(Settings.PreviouslySearchedParamPopulation, false);
        }

        private void createEntity_Click(object sender, EventArgs e)
        {
            if (entityName.Text == "")
            {
                MessageBox.Show("Please enter an entity name!", "No name.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (functionTypeList1.SelectedItem == null)
            {
                MessageBox.Show("Please select a function type!", "No type.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            FunctionType function = (FunctionType)Enum.Parse(typeof(FunctionType), functionTypeList1.SelectedItem.Text);

            //A composite can only have one PhysicsSystem
            if (function == FunctionType.PhysicsSystem && _composite.functions.FirstOrDefault(o => o.function == FunctionType.PhysicsSystem) != null)
            {
                MessageBox.Show("You are trying to add a PhysicsSystem entity to a composite that already has one applied.", "PhysicsSystem error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            //A composite can only have one EnvironmentModelReference
            if (function == FunctionType.EnvironmentModelReference && _composite.functions.FirstOrDefault(o => o.function == FunctionType.EnvironmentModelReference) != null)
            {
                MessageBox.Show("You are trying to add a EnvironmentModelReference entity to a composite that already has one applied.", "EnvironmentModelReference error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Singleton.OnEntityAddPending?.Invoke();
            Entity newEntity = _composite.AddFunction(function);

            if (addDefaultParams.Checked)
            {
                Content.Level.Commands.Utils.AddAllDefaultParameters(newEntity, _composite);
                newEntity.RemoveParameter("delete_me");
            }

            Content.Level.Commands.Utils.SetEntityName(_composite, newEntity, entityName.Text);
            SettingsManager.SetString(Settings.PreviouslySelectedFunctionType, function.ToString());
            SettingsManager.SetBool(Settings.PreviouslySearchedParamPopulation, addDefaultParams.Checked);

            Singleton.OnEntityAdded?.Invoke(newEntity);
            this.Close();
        }

        private void CreateEntityOnEnterKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                createEntity.PerformClick();
        }

        private void helpBtn_Click(object sender, EventArgs e)
        {
            Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
            Process.Start("https://opencage.co.uk/docs/cathode-entities/#entities");
        }

        private void functionTypeList_SelectedIndexChanged()
        {
            if (functionTypeList1.SelectedItem == null)
                return;
            
            if (entityName.Text == "" || Enum.TryParse<FunctionType>(entityName.Text, out FunctionType type))
                entityName.Text = functionTypeList1.SelectedItem.Text;
        }
    }
}

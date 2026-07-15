using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;
using OpenCAGE.Popups.UserControls;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class AddEntity_Variable : BaseWindow
    {
        private Composite _composite;

        public AddEntity_Variable(Composite composite, bool flowgraphMode) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();

            _composite = composite;

            variableEnumType.BeginUpdate();
            variableEnumType.Items.Clear();
            variableEnumType.Items.AddRange(Enum.GetNames(typeof(EnumType)).OrderBy(o => o).ToArray());
            variableEnumType.EndUpdate();
            variableEnumType.SelectedIndex = SettingsManager.GetInteger(Settings.PrevVariableType_Enum);

            variableEnumStringType.BeginUpdate();
            variableEnumStringType.Items.Clear();
            variableEnumStringType.Items.AddRange(Enum.GetNames(typeof(EnumStringType)).OrderBy(o => o).ToArray());
            variableEnumStringType.EndUpdate();
            variableEnumStringType.SelectedIndex = SettingsManager.GetInteger(Settings.PrevVariableType_EnumString);

            variableType.BeginUpdate();
            variableType.Items.Clear();
            foreach (CompositePinType pinType in EnumExtensions.GetValuesInDeclarationOrder<CompositePinType>())
            {
                if (pinType == CompositePinType.CompositeInputVariablePin || pinType == CompositePinType.CompositeOutputVariablePin)
                    continue;

                variableType.Items.Add(pinType.ToUIString()); 
            }
            variableType.EndUpdate();
            variableType.SelectedIndex = SettingsManager.GetInteger(Settings.PrevVariableType);

            variableName.Select();
        }

        private void createEntity(object sender, EventArgs e)
        {
            if (variableName.Text == "")
            {
                MessageBox.Show("Please enter a name!", "No name.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShortGuid name_guid = ShortGuidUtils.Generate(variableName.Text);
            foreach (VariableEntity varEnt in _composite.variables)
            {
                if (varEnt.name == name_guid)
                {
                    MessageBox.Show("A parameter within this composite already has the same name, please pick another!", "Duplicate name.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            //TODO: verify the ones that aren't listed here are definitely float

            CompositePinType pinType = variableType.SelectedItem.ToString().ToCompositePinType();
            DataType datatype = pinType.GetDataType();
            ShortGuid enumType = new ShortGuid(0);
            switch (pinType)
            {
                case CompositePinType.CompositeInputEnumVariablePin:
                case CompositePinType.CompositeOutputEnumVariablePin:
                    enumType = ShortGuidUtils.Generate(variableEnumType.SelectedItem.ToString());
                    break;
                case CompositePinType.CompositeInputEnumStringVariablePin:
                case CompositePinType.CompositeOutputEnumStringVariablePin:
                    enumType = ShortGuidUtils.Generate(variableEnumStringType.SelectedItem.ToString());
                    break;
            }

            Singleton.OnEntityAddPending?.Invoke();
            ShortGuid entityID = ShortGuidUtils.GenerateRandom();
            VariableEntity newEntity = _composite.AddVariable(variableName.Text, datatype);
            Content.Level.Commands.Utils.SetPinInfo(_composite, new CompositePinInfoTable.PinInfo()
            {
                VariableGUID = newEntity.shortGUID,
                PinTypeGUID = new ShortGuid((uint)pinType),
                PinEnumTypeGUID = enumType
            });
            Content.Level.Commands.Utils.AddAllDefaultParameters(newEntity, _composite, true, ParameterVariant.REFERENCE_PIN | ParameterVariant.TARGET_PIN | ParameterVariant.STATE_PARAMETER | ParameterVariant.INPUT_PIN | ParameterVariant.OUTPUT_PIN | ParameterVariant.PARAMETER | ParameterVariant.INTERNAL | ParameterVariant.METHOD_FUNCTION | ParameterVariant.METHOD_PIN);
            if (newEntity.parameters[0].content.dataType == DataType.ENUM)
            {
                cEnum enumParam = (cEnum)newEntity.parameters[0].content;
                enumParam.enumID = enumType; //todo: this should be applied above...
                enumParam.enumIndex = Content.Level.Commands.Utils.GetEnum(enumType).Entries[0].Index;
            }
            EntityPaletteRecent.RecordVariable(pinType);
            Singleton.OnEntityAdded?.Invoke(newEntity);

            this.Close();
        }

        private void OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                createVariable.PerformClick();
        }

        private void entityVariant_SelectedIndexChanged(object sender, EventArgs e)
        {
            CompositePinType type = variableType.SelectedItem.ToString().ToCompositePinType();

            bool isEnum = type == CompositePinType.CompositeInputEnumVariablePin || type == CompositePinType.CompositeOutputEnumVariablePin;
            variableEnumType.Enabled = isEnum;
            bool isEnumString = type == CompositePinType.CompositeInputEnumStringVariablePin || type == CompositePinType.CompositeOutputEnumStringVariablePin;
            variableEnumStringType.Visible = isEnumString;
            variableEnumStringType.Enabled = isEnumString;
            SettingsManager.SetInteger(Settings.PrevVariableType, variableType.SelectedIndex);
        }

        private void variableEnumType_SelectedIndexChanged(object sender, EventArgs e)
        {
            SettingsManager.SetInteger(Settings.PrevVariableType_Enum, variableEnumType.SelectedIndex);
        }

        private void variableEnumStringType_SelectedIndexChanged(object sender, EventArgs e)
        {
            SettingsManager.SetInteger(Settings.PrevVariableType_EnumString, variableEnumStringType.SelectedIndex);
        }
    }
}

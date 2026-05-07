    using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;

namespace OpenCAGE
{
    public partial class AddCustomParameter : BaseWindow
    {
        public Action<string, DataType> OnSelected;
        
        EntityInspector _entityDisplay;

        public AddCustomParameter(EntityInspector entityDisplay) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            _entityDisplay = entityDisplay;

            InitializeComponent();

            param_name.BeginUpdate();
            param_name.Items.Clear();
            param_name.Items.AddRange(entityDisplay.Content.EditorUtils.GenerateParameterListAsString(entityDisplay.Entity, entityDisplay.Composite).ToArray());
            param_name.EndUpdate();

            param_datatype.BeginUpdate();
            param_datatype.Items.Clear();
            foreach (DataType datatype in EnumExtensions.GetValuesInDeclarationOrder<DataType>())
            {
                if (datatype == DataType.NONE)
                    continue;

                param_datatype.Items.Add(datatype.ToUIString());
            }
            param_datatype.EndUpdate();
            param_datatype.SelectedIndex = 0;

            param_name.AutoSelectOff();
            param_name.Select();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (param_name.Text == "") 
                return;

            OnSelected?.Invoke(param_name.Text, param_datatype.Text.ToDataType());
            this.Close();
        }

        private void param_name_TextChanged(object sender, EventArgs e)
        {
            param_name_SelectedIndexChanged(null, null);
        }
        private void param_name_SelectedIndexChanged(object sender, EventArgs e)
        {
            (ParameterVariant?, DataType?, ShortGuid) metadata = Content.Level.Commands.Utils.GetParameterMetadata(_entityDisplay.Entity, param_name.Text, _entityDisplay.Composite);
            if (metadata.Item2 != null)
                param_datatype.Text = metadata.Item2.Value.ToUIString();
        }
    }
}

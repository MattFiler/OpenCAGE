using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.ConfigEditors
{
    public partial class ScriptReadableVariableEditor : BaseWindow
    {
        public ScriptReadableVariableEditor() : base()
        {
            InitializeComponent();

            //SCRIPT_READABLE_VARIABLES.XML -> this is used by PACKAGES, where the values can be defined within script_variable_values, and then read by the ExternalVariableBool entity - useful?

            // todo - when i implement this i need to add it to the backup tool
        }
    }
}

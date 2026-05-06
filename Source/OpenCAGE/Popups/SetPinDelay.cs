using CATHODE.Scripting.Internal;
using OpenCAGE.Popups.Base;
using OpenCAGE;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    public partial class SetPinDelay : BaseWindow
    {
        public Action<Entity, string, float, PinLocation> OnDelaySet;

        private PinLocation _loc;
        private string _param;
        private Entity _ent;

        public SetPinDelay(Entity entity, string parameter, float currentDelay, PinLocation location) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            _ent = entity;
            _param = parameter;
            _loc = location;

            InitializeComponent();

            numericUpDown1.DecimalPlaces = 6;
            numericUpDown1.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStep);
            numericUpDown1.Maximum = (decimal)3.4E+28m;
            numericUpDown1.Minimum = (decimal)-3.4E+28m;

            numericUpDown1.Value = (decimal)currentDelay;

            this.Text = "Set '" + parameter + "' Delay";
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OnDelaySet?.Invoke(_ent, _param, (float)numericUpDown1.Value, _loc);
            this.Close();
        }
    }
}

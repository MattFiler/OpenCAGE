using OpenCAGE.Popups.Base;
using OpenCAGE.Properties;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class SetNodeColours : BaseWindow
    {
        public SetNodeColours() : base()
        {
            InitializeComponent();
            UpdateColourPreviews();
        }
        
        private void UpdateColourPreviews()
        {
            setFuncColourText.BackColor = Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_FunctionText));
            setFuncColourNode.BackColor = Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_FunctionNode));
            setFuncColourNodeBtm.BackColor = Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_FunctionNodeBottom));
            setProxyColourText.BackColor = Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_ProxyText));
            setProxyColourNode.BackColor = Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_ProxyNode));
            setProxyColourNodeBtm.BackColor = Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_ProxyNodeBottom));
            setAliasColourText.BackColor = Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_AliasText));
            setAliasColourNode.BackColor = Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_AliasNode));
            setAliasColourNodeBtm.BackColor = Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_AliasNodeBottom));
            setInstanceColourText.BackColor = Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_InstanceText));
            setInstanceColourNode.BackColor = Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_InstanceNode));
            setInstanceColourNodeBtm.BackColor = Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_InstanceNodeBottom));
            setVarColourText.BackColor = Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_VariableText));
            setVarColourNode.BackColor = Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_VariableNode));
        }

        private Color SetColour(string setting)
        {
            Color orig = Color.FromArgb(SettingsManager.GetInteger(setting));

            ColorDialog colourPicker = new ColorDialog();
            colourPicker.Color = orig;
            if (colourPicker.ShowDialog() == DialogResult.OK)
            {
                SettingsManager.SetInteger(setting, colourPicker.Color.ToArgb());
                Singleton.OnNodeStyleChanged?.Invoke();
                return colourPicker.Color;
            }

            return orig;
        }

        private void setFuncColourText_Click(object sender, EventArgs e)
        {
            setFuncColourText.BackColor = SetColour(Singleton.Settings.NodeColour_FunctionText);
        }

        private void setFuncColourNode_Click(object sender, EventArgs e)
        {
            setFuncColourNode.BackColor = SetColour(Singleton.Settings.NodeColour_FunctionNode);
        }

        private void setFuncColourNodeBtm_Click(object sender, EventArgs e)
        {
            setFuncColourNodeBtm.BackColor = SetColour(Singleton.Settings.NodeColour_FunctionNodeBottom);
        }

        private void setInstanceColourText_Click(object sender, EventArgs e)
        {
            setInstanceColourText.BackColor = SetColour(Singleton.Settings.NodeColour_InstanceText);
        }

        private void setInstanceColourNode_Click(object sender, EventArgs e)
        {
            setInstanceColourNode.BackColor = SetColour(Singleton.Settings.NodeColour_InstanceNode);
        }

        private void setInstanceColourNodeBtm_Click(object sender, EventArgs e)
        {
            setInstanceColourNodeBtm.BackColor = SetColour(Singleton.Settings.NodeColour_InstanceNodeBottom);
        }

        private void setProxyColourText_Click(object sender, EventArgs e)
        {
            setProxyColourText.BackColor = SetColour(Singleton.Settings.NodeColour_ProxyText);
        }

        private void setProxyColourNode_Click(object sender, EventArgs e)
        {
            setProxyColourNode.BackColor = SetColour(Singleton.Settings.NodeColour_ProxyNode);
        }

        private void setProxyColourNodeBtm_Click(object sender, EventArgs e)
        {
            setProxyColourNodeBtm.BackColor = SetColour(Singleton.Settings.NodeColour_ProxyNodeBottom);
        }

        private void setAliasColourText_Click(object sender, EventArgs e)
        {
            setAliasColourText.BackColor = SetColour(Singleton.Settings.NodeColour_AliasText);
        }

        private void setAliasColourNode_Click(object sender, EventArgs e)
        {
            setAliasColourNode.BackColor = SetColour(Singleton.Settings.NodeColour_AliasNode);
        }

        private void setAliasColourNodeBtm_Click(object sender, EventArgs e)
        {
            setAliasColourNodeBtm.BackColor = SetColour(Singleton.Settings.NodeColour_AliasNodeBottom);
        }

        private void setVarColourText_Click(object sender, EventArgs e)
        {
            setVarColourText.BackColor = SetColour(Singleton.Settings.NodeColour_VariableText);
        }

        private void setVarColourNode_Click(object sender, EventArgs e)
        {
            setVarColourNode.BackColor = SetColour(Singleton.Settings.NodeColour_VariableNode);
        }

        private void resetBtn_Click(object sender, EventArgs e)
        {
            SettingsManager.SetInteger(Singleton.Settings.NodeColour_FunctionNode, Color.FromArgb(30, 144, 255).ToArgb());
            SettingsManager.SetInteger(Singleton.Settings.NodeColour_FunctionNodeBottom, Color.FromArgb(10, 109, 157).ToArgb());
            SettingsManager.SetInteger(Singleton.Settings.NodeColour_FunctionText, Color.White.ToArgb());

            SettingsManager.SetInteger(Singleton.Settings.NodeColour_AliasNode, Color.FromArgb(255, 114, 30).ToArgb());
            SettingsManager.SetInteger(Singleton.Settings.NodeColour_AliasNodeBottom, Color.FromArgb(196, 76, 29).ToArgb());
            SettingsManager.SetInteger(Singleton.Settings.NodeColour_AliasText, Color.White.ToArgb());

            SettingsManager.SetInteger(Singleton.Settings.NodeColour_ProxyNode, Color.FromArgb(35, 196, 22).ToArgb());
            SettingsManager.SetInteger(Singleton.Settings.NodeColour_ProxyNodeBottom, Color.FromArgb(9, 153, 72).ToArgb());
            SettingsManager.SetInteger(Singleton.Settings.NodeColour_ProxyText, Color.White.ToArgb());

            SettingsManager.SetInteger(Singleton.Settings.NodeColour_InstanceNode, Color.FromArgb(195, 30, 255).ToArgb());
            SettingsManager.SetInteger(Singleton.Settings.NodeColour_InstanceNodeBottom, Color.FromArgb(118, 10, 157).ToArgb());
            SettingsManager.SetInteger(Singleton.Settings.NodeColour_InstanceText, Color.White.ToArgb());

            SettingsManager.SetInteger(Singleton.Settings.NodeColour_VariableNode, Color.Red.ToArgb());
            SettingsManager.SetInteger(Singleton.Settings.NodeColour_VariableText, Color.White.ToArgb());

            UpdateColourPreviews();
            Singleton.OnNodeStyleChanged?.Invoke();
        }
    }
}

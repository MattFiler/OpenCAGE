using CATHODE;
using CommandsEditor.ConfigEditors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace CommandsEditor.Popups.Configuration_Editors.User_Controls
{
    public partial class ViewconeDifficultySet : UserControl
    {
        public ViewconeDifficultySet()
        {
            InitializeComponent();
        }

        public void Populate(List<BML> bmls, string type)
        {
            string[] elementPath = new string[] { "DifficultySetting", "ViewconeSets", type, "Close" };
            vc_Close.Populate(bmls, elementPath);
            elementPath[elementPath.Length - 1] = "Focused";
            vc_Focused.Populate(bmls, elementPath);
            elementPath[elementPath.Length - 1] = "Normal";
            vc_Normal.Populate(bmls, elementPath);
            elementPath[elementPath.Length - 1] = "Peripheral";
            vc_Peripheral.Populate(bmls, elementPath);
        }

        public void Save(XmlElement viewcone)
        {
            vc_Close.Save(viewcone["Close"]);
            vc_Focused.Save(viewcone["Focused"]);
            vc_Normal.Save(viewcone["Normal"]);
            vc_Peripheral.Save(viewcone["Peripheral"]);
        }
    }
}

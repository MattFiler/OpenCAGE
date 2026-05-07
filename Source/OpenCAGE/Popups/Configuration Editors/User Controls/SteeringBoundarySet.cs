using CATHODE.Enums;
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

namespace OpenCAGE.ConfigEditors
{
    public partial class SteeringBoundarySet : UserControl
    {
        private string _setName;

        public SteeringBoundarySet()
        {
            InitializeComponent();
        }

        public void Populate(XmlElement boundaries)
        {
            _setName = boundaries.Name;

            int i = 0;
            foreach (XmlElement boundary in boundaries)
            {
                tabControl1.TabPages[i].Controls.OfType<SteeringBoundary>().FirstOrDefault().Populate(boundary);
                i++;
            }
            tabControl1.SelectedIndex = 0;
        }

        public void Save(XmlDocument doc, XmlElement parent)
        {
            parent[_setName].RemoveAll();

            foreach (TabPage page in tabControl1.TabPages)
            {
                XmlElement boundary = doc.CreateElement("steeringBoundary");
                page.Controls.OfType<SteeringBoundary>().FirstOrDefault().Save(boundary);
                parent[_setName].AppendChild(boundary);
            }
        }
    }
}

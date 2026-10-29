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
    public partial class SteeringBoundary : UserControl
    {
        public SteeringBoundary()
        {
            InitializeComponent();
            ConfigEditorUtils.ExpandNumericRanges(this.Controls);
        }

        public void Populate(XmlElement boundary)
        {
            ConfigEditorUtils.SetNumericFromText(linearVelocity, boundary.GetAttribute("linearVelocity"));
            ConfigEditorUtils.SetNumericFromText(linearAcceleration, boundary.GetAttribute("linearAcceleration"));
            ConfigEditorUtils.SetNumericFromText(maxAngularVelocity, boundary.GetAttribute("maxAngularVelocity"));
            ConfigEditorUtils.SetNumericFromText(angularAcceleration, boundary.GetAttribute("angularAcceleration"));
            ConfigEditorUtils.SetNumericFromText(stoppingDistance, boundary.GetAttribute("stoppingDistance"));
            ConfigEditorUtils.SetNumericFromText(corneringWeight, boundary.GetAttribute("corneringWeight"));
            ConfigEditorUtils.SetNumericFromText(corneringPenalty, boundary.GetAttribute("corneringPenalty"));
            ConfigEditorUtils.SetNumericFromText(maxAngularWarping, boundary.GetAttribute("maxAngularWarping"));
            ConfigEditorUtils.SetNumericFromText(maxLinearWarping, boundary.GetAttribute("maxLinearWarping"));
        }

        public void Save(XmlElement parent)
        {
            parent.SetAttribute("linearVelocity", ConfigEditorUtils.NumberText(linearVelocity));
            parent.SetAttribute("linearAcceleration", ConfigEditorUtils.NumberText(linearAcceleration));
            parent.SetAttribute("maxAngularVelocity", ConfigEditorUtils.NumberText(maxAngularVelocity));
            parent.SetAttribute("angularAcceleration", ConfigEditorUtils.NumberText(angularAcceleration));
            parent.SetAttribute("stoppingDistance", ConfigEditorUtils.NumberText(stoppingDistance));
            parent.SetAttribute("corneringWeight", ConfigEditorUtils.NumberText(corneringWeight));
            parent.SetAttribute("corneringPenalty", ConfigEditorUtils.NumberText(corneringPenalty));
            parent.SetAttribute("maxAngularWarping", ConfigEditorUtils.NumberText(maxAngularWarping));
            parent.SetAttribute("maxLinearWarping", ConfigEditorUtils.NumberText(maxLinearWarping));
        }
    }
}

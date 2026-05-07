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
        }

        public void Populate(XmlElement boundary)
        {
            linearVelocity.Text = boundary.GetAttribute("linearVelocity");
            linearAcceleration.Text = boundary.GetAttribute("linearAcceleration");
            maxAngularVelocity.Text = boundary.GetAttribute("maxAngularVelocity");
            angularAcceleration.Text = boundary.GetAttribute("angularAcceleration");
            stoppingDistance.Text = boundary.GetAttribute("stoppingDistance");
            corneringWeight.Text = boundary.GetAttribute("corneringWeight");
            corneringPenalty.Text = boundary.GetAttribute("corneringPenalty");
            maxAngularWarping.Text = boundary.GetAttribute("maxAngularWarping");
            maxLinearWarping.Text = boundary.GetAttribute("maxLinearWarping");
        }

        public void Save(XmlElement parent)
        {
            parent.SetAttribute("linearVelocity", linearVelocity.Text);
            parent.SetAttribute("linearAcceleration", linearAcceleration.Text);
            parent.SetAttribute("maxAngularVelocity", maxAngularVelocity.Text);
            parent.SetAttribute("angularAcceleration", angularAcceleration.Text);
            parent.SetAttribute("stoppingDistance", stoppingDistance.Text);
            parent.SetAttribute("corneringWeight", corneringWeight.Text);
            parent.SetAttribute("corneringPenalty", corneringPenalty.Text);
            parent.SetAttribute("maxAngularWarping", maxAngularWarping.Text);
            parent.SetAttribute("maxLinearWarping", maxLinearWarping.Text);
        }
    }
}

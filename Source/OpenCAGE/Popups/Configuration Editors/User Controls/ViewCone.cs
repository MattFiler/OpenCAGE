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
    public partial class ViewCone : UserControl
    {
        private string _name;

        public ViewCone()
        {
            InitializeComponent();
        }

        public void Populate(XmlElement viewcone)
        {
            _name = viewcone["ViewconeSettings_type"].InnerText;
            Length.Text = viewcone["Length"].InnerText;
            SmokeLengthModifier.Text = viewcone["SmokeLengthModifier"].InnerText;
            VerticalAngle.Text = viewcone["VerticalAngle"].InnerText;
            HorizontalAngle.Text = viewcone["HorizontalAngle"].InnerText;
            ExposureEffectLower.Text = viewcone["ExposureEffectLower"].InnerText;
            ExposureEffectUpper.Text = viewcone["ExposureEffectUpper"].InnerText;
            StanceEffectLower.Text = viewcone["StanceEffectLower"].InnerText;
            StanceEffectUpper.Text = viewcone["StanceEffectUpper"].InnerText;
            MovementEffectLower.Text = viewcone["MovementEffectLower"].InnerText;
            MovementEffectUpper.Text = viewcone["MovementEffectUpper"].InnerText;
            SmokeEffectLower.Text = viewcone["SmokeEffectLower"].InnerText;
            SmokeEffectUpper.Text = viewcone["SmokeEffectUpper"].InnerText;
            DistanceEffectLower.Text = viewcone["DistanceEffectLower"].InnerText;
            DistanceEffectUpper.Text = viewcone["DistanceEffectUpper"].InnerText;
            Light_meter_dark_level.Text = viewcone["Light_meter_dark_level"].InnerText;
            Light_meter_partially_lit_level.Text = viewcone["Light_meter_partially_lit_level"].InnerText;
            Light_meter_fully_lit_level.Text = viewcone["Light_meter_fully_lit_level"].InnerText;
        }

        public void Save(XmlElement viewcone)
        {
            ConfigEditorUtils.EnsureChildElements(viewcone, "ViewconeSettings_type").InnerText = _name;
            ConfigEditorUtils.EnsureChildElements(viewcone, "Length").InnerText = Length.Text;
            ConfigEditorUtils.EnsureChildElements(viewcone, "SmokeLengthModifier").InnerText = SmokeLengthModifier.Text;
            ConfigEditorUtils.EnsureChildElements(viewcone, "VerticalAngle").InnerText = VerticalAngle.Text;
            ConfigEditorUtils.EnsureChildElements(viewcone, "HorizontalAngle").InnerText = HorizontalAngle.Text;
            ConfigEditorUtils.EnsureChildElements(viewcone, "ExposureEffectLower").InnerText = ExposureEffectLower.Text;
            ConfigEditorUtils.EnsureChildElements(viewcone, "ExposureEffectUpper").InnerText = ExposureEffectUpper.Text;
            ConfigEditorUtils.EnsureChildElements(viewcone, "StanceEffectLower").InnerText = StanceEffectLower.Text;
            ConfigEditorUtils.EnsureChildElements(viewcone, "StanceEffectUpper").InnerText = StanceEffectUpper.Text;
            ConfigEditorUtils.EnsureChildElements(viewcone, "MovementEffectLower").InnerText = MovementEffectLower.Text;
            ConfigEditorUtils.EnsureChildElements(viewcone, "MovementEffectUpper").InnerText = MovementEffectUpper.Text;
            ConfigEditorUtils.EnsureChildElements(viewcone, "SmokeEffectLower").InnerText = SmokeEffectLower.Text;
            ConfigEditorUtils.EnsureChildElements(viewcone, "SmokeEffectUpper").InnerText = SmokeEffectUpper.Text;
            ConfigEditorUtils.EnsureChildElements(viewcone, "DistanceEffectLower").InnerText = DistanceEffectLower.Text;
            ConfigEditorUtils.EnsureChildElements(viewcone, "DistanceEffectUpper").InnerText = DistanceEffectUpper.Text;
            ConfigEditorUtils.EnsureChildElements(viewcone, "Light_meter_dark_level").InnerText = Light_meter_dark_level.Text;
            ConfigEditorUtils.EnsureChildElements(viewcone, "Light_meter_partially_lit_level").InnerText = Light_meter_partially_lit_level.Text;
            ConfigEditorUtils.EnsureChildElements(viewcone, "Light_meter_fully_lit_level").InnerText = Light_meter_fully_lit_level.Text;
        }
    }
}

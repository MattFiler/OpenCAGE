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
            ConfigEditorUtils.ExpandNumericRanges(this.Controls);
        }

        public void Populate(XmlElement viewcone)
        {
            _name = viewcone["ViewconeSettings_type"].InnerText;
            ConfigEditorUtils.SetNumericFromText(Length, viewcone["Length"].InnerText);
            ConfigEditorUtils.SetNumericFromText(SmokeLengthModifier, viewcone["SmokeLengthModifier"].InnerText);
            ConfigEditorUtils.SetNumericFromText(VerticalAngle, viewcone["VerticalAngle"].InnerText);
            ConfigEditorUtils.SetNumericFromText(HorizontalAngle, viewcone["HorizontalAngle"].InnerText);
            ConfigEditorUtils.SetNumericFromText(ExposureEffectLower, viewcone["ExposureEffectLower"].InnerText);
            ConfigEditorUtils.SetNumericFromText(ExposureEffectUpper, viewcone["ExposureEffectUpper"].InnerText);
            ConfigEditorUtils.SetNumericFromText(StanceEffectLower, viewcone["StanceEffectLower"].InnerText);
            ConfigEditorUtils.SetNumericFromText(StanceEffectUpper, viewcone["StanceEffectUpper"].InnerText);
            ConfigEditorUtils.SetNumericFromText(MovementEffectLower, viewcone["MovementEffectLower"].InnerText);
            ConfigEditorUtils.SetNumericFromText(MovementEffectUpper, viewcone["MovementEffectUpper"].InnerText);
            ConfigEditorUtils.SetNumericFromText(SmokeEffectLower, viewcone["SmokeEffectLower"].InnerText);
            ConfigEditorUtils.SetNumericFromText(SmokeEffectUpper, viewcone["SmokeEffectUpper"].InnerText);
            ConfigEditorUtils.SetNumericFromText(DistanceEffectLower, viewcone["DistanceEffectLower"].InnerText);
            ConfigEditorUtils.SetNumericFromText(DistanceEffectUpper, viewcone["DistanceEffectUpper"].InnerText);
            ConfigEditorUtils.SetNumericFromText(Light_meter_dark_level, viewcone["Light_meter_dark_level"].InnerText);
            ConfigEditorUtils.SetNumericFromText(Light_meter_partially_lit_level, viewcone["Light_meter_partially_lit_level"].InnerText);
            ConfigEditorUtils.SetNumericFromText(Light_meter_fully_lit_level, viewcone["Light_meter_fully_lit_level"].InnerText);
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

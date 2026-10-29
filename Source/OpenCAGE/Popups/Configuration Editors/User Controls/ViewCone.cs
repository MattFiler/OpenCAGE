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
            ConfigEditorUtils.EnsureChildElements(viewcone, "Length").InnerText = ConfigEditorUtils.NumberText(Length);
            ConfigEditorUtils.EnsureChildElements(viewcone, "SmokeLengthModifier").InnerText = ConfigEditorUtils.NumberText(SmokeLengthModifier);
            ConfigEditorUtils.EnsureChildElements(viewcone, "VerticalAngle").InnerText = ConfigEditorUtils.NumberText(VerticalAngle);
            ConfigEditorUtils.EnsureChildElements(viewcone, "HorizontalAngle").InnerText = ConfigEditorUtils.NumberText(HorizontalAngle);
            ConfigEditorUtils.EnsureChildElements(viewcone, "ExposureEffectLower").InnerText = ConfigEditorUtils.NumberText(ExposureEffectLower);
            ConfigEditorUtils.EnsureChildElements(viewcone, "ExposureEffectUpper").InnerText = ConfigEditorUtils.NumberText(ExposureEffectUpper);
            ConfigEditorUtils.EnsureChildElements(viewcone, "StanceEffectLower").InnerText = ConfigEditorUtils.NumberText(StanceEffectLower);
            ConfigEditorUtils.EnsureChildElements(viewcone, "StanceEffectUpper").InnerText = ConfigEditorUtils.NumberText(StanceEffectUpper);
            ConfigEditorUtils.EnsureChildElements(viewcone, "MovementEffectLower").InnerText = ConfigEditorUtils.NumberText(MovementEffectLower);
            ConfigEditorUtils.EnsureChildElements(viewcone, "MovementEffectUpper").InnerText = ConfigEditorUtils.NumberText(MovementEffectUpper);
            ConfigEditorUtils.EnsureChildElements(viewcone, "SmokeEffectLower").InnerText = ConfigEditorUtils.NumberText(SmokeEffectLower);
            ConfigEditorUtils.EnsureChildElements(viewcone, "SmokeEffectUpper").InnerText = ConfigEditorUtils.NumberText(SmokeEffectUpper);
            ConfigEditorUtils.EnsureChildElements(viewcone, "DistanceEffectLower").InnerText = ConfigEditorUtils.NumberText(DistanceEffectLower);
            ConfigEditorUtils.EnsureChildElements(viewcone, "DistanceEffectUpper").InnerText = ConfigEditorUtils.NumberText(DistanceEffectUpper);
            ConfigEditorUtils.EnsureChildElements(viewcone, "Light_meter_dark_level").InnerText = ConfigEditorUtils.NumberText(Light_meter_dark_level);
            ConfigEditorUtils.EnsureChildElements(viewcone, "Light_meter_partially_lit_level").InnerText = ConfigEditorUtils.NumberText(Light_meter_partially_lit_level);
            ConfigEditorUtils.EnsureChildElements(viewcone, "Light_meter_fully_lit_level").InnerText = ConfigEditorUtils.NumberText(Light_meter_fully_lit_level);
        }
    }
}

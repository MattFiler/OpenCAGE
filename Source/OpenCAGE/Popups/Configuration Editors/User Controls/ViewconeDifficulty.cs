using CATHODE;
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
    public partial class ViewconeDifficulty : UserControl
    {
        public ViewconeDifficulty()
        {
            InitializeComponent();
            ConfigEditorUtils.ExpandNumericRanges(this.Controls);
        }

        public void Populate(List<BML> bmls, string[] elementPath)
        {
            List<string> elementPathLst = new List<string>(elementPath);
            elementPathLst.Add("visual_sense_exposure_effect_lower_modifier");
            ConfigEditorUtils.SetNumber(bmls, visual_sense_exposure_effect_lower_modifier, elementPathLst.ToArray());
            elementPathLst[elementPathLst.Count - 1] = "visual_sense_exposure_effect_upper_modifier";
            ConfigEditorUtils.SetNumber(bmls, visual_sense_exposure_effect_upper_modifier, elementPathLst.ToArray());
            elementPathLst[elementPathLst.Count - 1] = "visual_sense_stance_effect_lower_modifier";
            ConfigEditorUtils.SetNumber(bmls, visual_sense_stance_effect_lower_modifier, elementPathLst.ToArray());
            elementPathLst[elementPathLst.Count - 1] = "visual_sense_stance_effect_upper_modifier";
            ConfigEditorUtils.SetNumber(bmls, visual_sense_stance_effect_upper_modifier, elementPathLst.ToArray());
        }

        public void Save(XmlElement viewcone)
        {
            //A field no file in the template chain has is disabled, and still shows the previous set's value
            if (visual_sense_exposure_effect_lower_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(viewcone, "visual_sense_exposure_effect_lower_modifier").InnerText = ConfigEditorUtils.NumberText(visual_sense_exposure_effect_lower_modifier);
            if (visual_sense_exposure_effect_upper_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(viewcone, "visual_sense_exposure_effect_upper_modifier").InnerText = ConfigEditorUtils.NumberText(visual_sense_exposure_effect_upper_modifier);
            if (visual_sense_stance_effect_lower_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(viewcone, "visual_sense_stance_effect_lower_modifier").InnerText = ConfigEditorUtils.NumberText(visual_sense_stance_effect_lower_modifier);
            if (visual_sense_stance_effect_upper_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(viewcone, "visual_sense_stance_effect_upper_modifier").InnerText = ConfigEditorUtils.NumberText(visual_sense_stance_effect_upper_modifier);
        }
    }
}

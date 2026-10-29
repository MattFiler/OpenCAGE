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
using System.Windows.Media;
using System.Xml;

namespace OpenCAGE.ConfigEditors
{
    public partial class SenseType : UserControl
    {
        public SenseType()
        {
            InitializeComponent();
            ConfigEditorUtils.ExpandNumericRanges(this.Controls);
        }

        public void Populate(List<BML> configs, string setName, string typeName, params string[] pathPrefix)
        {
            ConfigEditorUtils.SetNumber(configs, min_raw_activation, pathPrefix.Concat(new[] { typeName + "_min_raw_activation_" + setName }).ToArray());
            ConfigEditorUtils.SetNumber(configs, max_raw_activation, pathPrefix.Concat(new[] { typeName + "_max_raw_activation_" + setName }).ToArray());
            ConfigEditorUtils.SetNumber(configs, combined_sense_min_raw_activation, pathPrefix.Concat(new[] { typeName + "_combined_sense_min_raw_activation_" + setName }).ToArray());
            ConfigEditorUtils.SetNumber(configs, combined_sense_max_raw_activation, pathPrefix.Concat(new[] { typeName + "_combined_sense_max_raw_activation_" + setName }).ToArray());
            ConfigEditorUtils.SetNumber(configs, lower_threshold, pathPrefix.Concat(new[] { typeName + "_lower_threshold_" + setName }).ToArray());
            ConfigEditorUtils.SetNumber(configs, upper_threshold, pathPrefix.Concat(new[] { typeName + "_upper_threshold_" + setName }).ToArray());
            ConfigEditorUtils.SetNumber(configs, decay_per_second, pathPrefix.Concat(new[] { typeName + "_decay_per_second_" + setName }).ToArray());
            ConfigEditorUtils.SetNumber(configs, min_activated_time, pathPrefix.Concat(new[] { typeName + "_min_activated_time_" + setName }).ToArray());
            ConfigEditorUtils.SetNumber(configs, positional_accuracy_scalar, pathPrefix.Concat(new[] { typeName + "_positional_accuracy_scalar_" + setName }).ToArray());
            ConfigEditorUtils.SetNumber(configs, activation_scalar, pathPrefix.Concat(new[] { typeName + "_activation_scalar_" + setName }).ToArray());
            ConfigEditorUtils.SetNumber(configs, combined_sense_activation_scalar, pathPrefix.Concat(new[] { typeName + "_combined_sense_activation_scalar_" + setName }).ToArray());
            ConfigEditorUtils.SetNumber(configs, activation_threshold, pathPrefix.Concat(new[] { typeName + "_activation_threshold_" + setName }).ToArray());
            ConfigEditorUtils.SetNumber(configs, trace_threshold, pathPrefix.Concat(new[] { typeName + "_trace_threshold_" + setName }).ToArray());
            ConfigEditorUtils.SetNumber(configs, last_sensed_expire_time, pathPrefix.Concat(new[] { typeName + "_last_sensed_expire_time_" + setName }).ToArray());
        }

        public void Save(XmlElement sense, string setName, string typeName)
        {
            if (min_raw_activation.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_min_raw_activation_" + setName).InnerText = ConfigEditorUtils.NumberText(min_raw_activation);
            if (max_raw_activation.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_max_raw_activation_" + setName).InnerText = ConfigEditorUtils.NumberText(max_raw_activation);
            if (combined_sense_min_raw_activation.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_combined_sense_min_raw_activation_" + setName).InnerText = ConfigEditorUtils.NumberText(combined_sense_min_raw_activation);
            if (combined_sense_max_raw_activation.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_combined_sense_max_raw_activation_" + setName).InnerText = ConfigEditorUtils.NumberText(combined_sense_max_raw_activation);
            if (lower_threshold.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_lower_threshold_" + setName).InnerText = ConfigEditorUtils.NumberText(lower_threshold);
            if (upper_threshold.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_upper_threshold_" + setName).InnerText = ConfigEditorUtils.NumberText(upper_threshold);
            if (decay_per_second.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_decay_per_second_" + setName).InnerText = ConfigEditorUtils.NumberText(decay_per_second);
            if (min_activated_time.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_min_activated_time_" + setName).InnerText = ConfigEditorUtils.NumberText(min_activated_time);
            if (positional_accuracy_scalar.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_positional_accuracy_scalar_" + setName).InnerText = ConfigEditorUtils.NumberText(positional_accuracy_scalar);
            if (activation_scalar.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_activation_scalar_" + setName).InnerText = ConfigEditorUtils.NumberText(activation_scalar);
            if (combined_sense_activation_scalar.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_combined_sense_activation_scalar_" + setName).InnerText = ConfigEditorUtils.NumberText(combined_sense_activation_scalar);
            if (activation_threshold.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_activation_threshold_" + setName).InnerText = ConfigEditorUtils.NumberText(activation_threshold);
            if (trace_threshold.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_trace_threshold_" + setName).InnerText = ConfigEditorUtils.NumberText(trace_threshold);
            if (last_sensed_expire_time.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_last_sensed_expire_time_" + setName).InnerText = ConfigEditorUtils.NumberText(last_sensed_expire_time);
        }
    }
}

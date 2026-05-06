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
            if (min_raw_activation.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_min_raw_activation_" + setName).InnerText = min_raw_activation.Text;
            if (max_raw_activation.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_max_raw_activation_" + setName).InnerText = max_raw_activation.Text;
            if (combined_sense_min_raw_activation.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_combined_sense_min_raw_activation_" + setName).InnerText = combined_sense_min_raw_activation.Text;
            if (combined_sense_max_raw_activation.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_combined_sense_max_raw_activation_" + setName).InnerText = combined_sense_max_raw_activation.Text;
            if (lower_threshold.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_lower_threshold_" + setName).InnerText = lower_threshold.Text;
            if (upper_threshold.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_upper_threshold_" + setName).InnerText = upper_threshold.Text;
            if (decay_per_second.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_decay_per_second_" + setName).InnerText = decay_per_second.Text;
            if (min_activated_time.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_min_activated_time_" + setName).InnerText = min_activated_time.Text;
            if (positional_accuracy_scalar.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_positional_accuracy_scalar_" + setName).InnerText = positional_accuracy_scalar.Text;
            if (activation_scalar.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_activation_scalar_" + setName).InnerText = activation_scalar.Text;
            if (combined_sense_activation_scalar.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_combined_sense_activation_scalar_" + setName).InnerText = combined_sense_activation_scalar.Text;
            if (activation_threshold.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_activation_threshold_" + setName).InnerText = activation_threshold.Text;
            if (trace_threshold.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_trace_threshold_" + setName).InnerText = trace_threshold.Text;
            if (last_sensed_expire_time.Enabled) ConfigEditorUtils.EnsureChildElements(sense, typeName + "_last_sensed_expire_time_" + setName).InnerText = last_sensed_expire_time.Text;
        }
    }
}

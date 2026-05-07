using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.ConfigEditors
{
    public partial class HairAndSkinShadingEditor : BaseWindow
    {
        public HairAndSkinShadingEditor() : base()
        {
            InitializeComponent();

            string[] skin = File.ReadAllLines(Singleton.PathToAI + "/DATA/SKIN_SHADING_SETTINGS.TXT");
            GetValue(skin, "scattering_radius", scattering_radius);
            GetValue(skin, "scattering_saturation", scattering_saturation);

            string[] hair = File.ReadAllLines(Singleton.PathToAI + "/DATA/HAIR_SHADING_SETTINGS.TXT");
            GetValue(hair, "alpha_threshold", alpha_threshold);
            GetValue(hair, "alpha_threshold_shadow", alpha_threshold_shadow);
            GetValue(hair, "primary_spec_level", primary_spec_level);
            GetValue(hair, "secondary_spec_level", secondary_spec_level);
            GetValue(hair, "primary_spec_width", primary_spec_width);
            GetValue(hair, "secondary_spec_width", secondary_spec_width);
            GetValue(hair, "spec_separation", spec_separation);
            GetValue(hair, "diffuse_level", diffuse_level);
            GetValue(hair, "base_absorption", base_absorption);
            GetValue(hair, "absorption_rate", absorption_rate);
            GetValue(hair, "ao_absorption", ao_absorption);
            GetValue(hair, "scatter_dist_rate", scatter_dist_rate);
            GetValue(hair, "occlusion_rate", occlusion_rate);
            GetValue(hair, "occlusion_bias", occlusion_bias);
            GetValue(hair, "occlusion_ao_infl", occlusion_ao_infl);
            GetValue(hair, "specular_occlusion", specular_occlusion);
            GetValue(hair, "specular_ao", specular_ao);
            GetValue(hair, "softening_length", softening_length);
            GetValue(hair, "softening_normal_bias", softening_normal_bias);
            GetValue(hair, "softening_distance_rate", softening_distance_rate);

            ConfigEditorUtils.Subscribe(this.Controls, Save);
            this.FormClosing += HairAndSkinShadingEditor_FormClosing;
            Singleton.OnResetConfigs += () => { this.Close(); };
        }

        private void HairAndSkinShadingEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.FormClosing -= HairAndSkinShadingEditor_FormClosing;
            ConfigEditorUtils.Unsubscribe(this.Controls, Save);
        }

        private void GetValue(string[] config, string name, NumericUpDown updown)
        {
            foreach(string value in config)
            {
                if (value.StartsWith(name + "="))
                {
                    updown.Value = Convert.ToDecimal(value.Substring(name.Length + 1));
                    break;
                }
            }
        }

        private void Save(object sender, EventArgs e)
        {
            List<string> skin = new List<string>();
            SetValue(skin, "scattering_radius", scattering_radius);
            SetValue(skin, "scattering_saturation", scattering_saturation);
            File.WriteAllLines(Singleton.PathToAI + "/DATA/SKIN_SHADING_SETTINGS.TXT", skin);

            List<string> hair = new List<string>();
            SetValue(hair, "alpha_threshold", alpha_threshold);
            SetValue(hair, "alpha_threshold_shadow", alpha_threshold_shadow);
            SetValue(hair, "primary_spec_level", primary_spec_level);
            SetValue(hair, "secondary_spec_level", secondary_spec_level);
            SetValue(hair, "primary_spec_width", primary_spec_width);
            SetValue(hair, "secondary_spec_width", secondary_spec_width);
            SetValue(hair, "spec_separation", spec_separation);
            SetValue(hair, "diffuse_level", diffuse_level);
            SetValue(hair, "base_absorption", base_absorption);
            SetValue(hair, "absorption_rate", absorption_rate);
            SetValue(hair, "ao_absorption", ao_absorption);
            SetValue(hair, "scatter_dist_rate", scatter_dist_rate);
            SetValue(hair, "occlusion_rate", occlusion_rate);
            SetValue(hair, "occlusion_bias", occlusion_bias);
            SetValue(hair, "occlusion_ao_infl", occlusion_ao_infl);
            SetValue(hair, "specular_occlusion", specular_occlusion);
            SetValue(hair, "specular_ao", specular_ao);
            SetValue(hair, "softening_length", softening_length);
            SetValue(hair, "softening_normal_bias", softening_normal_bias);
            SetValue(hair, "softening_distance_rate", softening_distance_rate);
            File.WriteAllLines(Singleton.PathToAI + "/DATA/HAIR_SHADING_SETTINGS.TXT", hair);

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void SetValue(List<string> config, string name, NumericUpDown updown)
        {
            config.Add(name + "=" + updown.Value.ToString());
        }
    }
}

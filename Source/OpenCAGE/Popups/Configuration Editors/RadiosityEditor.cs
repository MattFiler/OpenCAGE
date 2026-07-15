using OpenCAGE.Popups.Base;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.ConfigEditors
{
    public partial class RadiosityEditor : BaseWindow
    {
        public RadiosityEditor() : base()
        {
            InitializeComponent();
            ConfigEditorUtils.ExpandNumericRanges(this.Controls);
        }

        private void RadiosityEditor_Load(object sender, EventArgs e)
        {
            string[] lightingData = File.ReadAllLines(Singleton.PathToAI + @"\DATA\RADIOSITY_SETTINGS.TXT");
            for (int i = 0; i < lightingData.Length; i++) if (lightingData[i] != "") lightingData[i] = lightingData[i].Split('=')[1];

            ConfigEditorUtils.SetNumericFromText(gRadiosityEmissiveSurfaceScale, lightingData[1]);
            ConfigEditorUtils.SetNumericFromText(gRadiosityFirstBounceScale, lightingData[2]);
            ConfigEditorUtils.SetNumericFromText(gRadiosityMultiBounceScale, lightingData[3]);
            ConfigEditorUtils.SetNumericFromText(gRadiosityAlbedoOverbrightAmount, lightingData[4]);
            ConfigEditorUtils.SetNumericFromText(gRadiosityAlbedoSaturationAmount, lightingData[5]);
            ConfigEditorUtils.SetNumericFromText(gRadiositySpecularGlossScale, lightingData[6]);

            ConfigEditorUtils.Subscribe(this.Controls, Save);
            this.FormClosing += RadiosityEditor_FormClosing;
            Singleton.OnResetConfigs += () => { this.Close(); };
        }

        private void RadiosityEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(this.Controls, Save);
            this.FormClosing -= RadiosityEditor_FormClosing;
        }

        private void Save(object sender, EventArgs e)
        {
            File.WriteAllText(Singleton.PathToAI + @"\DATA\RADIOSITY_SETTINGS.TXT", 
                "settings_file_version=1" + Environment.NewLine +
                "gRadiosityEmissiveSurfaceScale=" + gRadiosityEmissiveSurfaceScale.Text + Environment.NewLine +
                "gRadiosityFirstBounceScale=" + gRadiosityFirstBounceScale.Text + Environment.NewLine +
                "gRadiosityMultiBounceScale=" + gRadiosityMultiBounceScale.Text + Environment.NewLine +
                "gRadiosityAlbedoOverbrightAmount=" + gRadiosityAlbedoOverbrightAmount.Text + Environment.NewLine +
                "gRadiosityAlbedoSaturationAmount=" + gRadiosityAlbedoSaturationAmount.Text + Environment.NewLine +
                "gRadiositySpecularGlossScale=" + gRadiositySpecularGlossScale.Text + Environment.NewLine +
                "gDeferredEmissiveSurfaceScale=1.487500" + Environment.NewLine +
                "gDeferredEmissiveSurfaceExponent=0.563500");

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void helpBtn_Click(object sender, EventArgs e)
        {
            Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
            Process.Start("https://opencage.co.uk/docs/configs/radiosity");
        }
    }
}

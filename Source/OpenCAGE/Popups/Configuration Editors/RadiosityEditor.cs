using OpenCAGE.Popups.Base;
using OpenCAGE.RuntimeUtilsConnection;
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
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/configs/radiosity/");
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
            CloseOnConfigReset();

            //The game reads these when a level loads; with Live Link connected each change reaches it at once
            LiveSettings.Attach(this, liveStatus, (LiveLink.SettingsFile.Radiosity, () => LiveSettings.Values(gRadiosityEmissiveSurfaceScale, gRadiosityFirstBounceScale,
                gRadiosityMultiBounceScale, gRadiosityAlbedoOverbrightAmount, gRadiosityAlbedoSaturationAmount, gRadiositySpecularGlossScale)));
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
                "gRadiosityEmissiveSurfaceScale=" + ConfigEditorUtils.NumberText(gRadiosityEmissiveSurfaceScale) + Environment.NewLine +
                "gRadiosityFirstBounceScale=" + ConfigEditorUtils.NumberText(gRadiosityFirstBounceScale) + Environment.NewLine +
                "gRadiosityMultiBounceScale=" + ConfigEditorUtils.NumberText(gRadiosityMultiBounceScale) + Environment.NewLine +
                "gRadiosityAlbedoOverbrightAmount=" + ConfigEditorUtils.NumberText(gRadiosityAlbedoOverbrightAmount) + Environment.NewLine +
                "gRadiosityAlbedoSaturationAmount=" + ConfigEditorUtils.NumberText(gRadiosityAlbedoSaturationAmount) + Environment.NewLine +
                "gRadiositySpecularGlossScale=" + ConfigEditorUtils.NumberText(gRadiositySpecularGlossScale) + Environment.NewLine +
                "gDeferredEmissiveSurfaceScale=1.487500" + Environment.NewLine +
                "gDeferredEmissiveSurfaceExponent=0.563500");
            LiveSettings.Changed(LiveLink.SettingsFile.Radiosity);

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }
    }
}

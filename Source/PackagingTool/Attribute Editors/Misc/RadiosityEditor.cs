/*
 * 
 * Created by Matt Filer
 * www.mattfiler.co.uk
 * 
 */

 using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PackagingTool
{
    public partial class RadiosityEditor : Form
    {
        //Main Directories
        string gameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz"); //Our game's dir

        //Common file paths
        string pathToLightingFile;
        string pathToSkinFile;
        string pathToHairFile;

        public RadiosityEditor()
        {
            InitializeComponent();

            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        //Save
        private void btnSave_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Set common file paths
            pathToLightingFile = gameDirectory + @"\DATA\RADIOSITY_SETTINGS.TXT";
            pathToSkinFile = gameDirectory + @"\DATA\SKIN_SHADING_SETTINGS.TXT";
            pathToHairFile = gameDirectory + @"\DATA\HAIR_SHADING_SETTINGS.TXT";


            //Compile data - RADIOSITY
            string radiosityToSave = 
                "settings_file_version=1\n" +
                "gRadiosityEmissiveSurfaceScale=" + gRadiosityEmissiveSurfaceScale.Text + "\n" +
                "gRadiosityFirstBounceScale=" + gRadiosityFirstBounceScale.Text + "\n" +
                "gRadiosityMultiBounceScale=" + gRadiosityMultiBounceScale.Text + "\n" +
                "gRadiosityAlbedoOverbrightAmount=" + gRadiosityAlbedoOverbrightAmount.Text + "\n" +
                "gRadiosityAlbedoSaturationAmount=" + gRadiosityAlbedoSaturationAmount.Text + "\n" +
                "gRadiositySpecularGlossScale=" + gRadiositySpecularGlossScale.Text + "\n" +
                "gDeferredEmissiveSurfaceScale=" + gDeferredEmissiveSurfaceScale.Text + "\n" +
                "gDeferredEmissiveSurfaceExponent=" + gDeferredEmissiveSurfaceExponent.Text;

            //Write data - RADIOSITY
            File.WriteAllText(pathToLightingFile, radiosityToSave);


            //compile data - SKIN_SHADING
            string skinToSave =
                "scattering_radius=" + scattering_radius.Text + "\n" +
                "scattering_saturation=" + scattering_saturation.Text;

            //Write data - SKIN_SHADING
            File.WriteAllText(pathToSkinFile, skinToSave);


            //compile data - HAIR_SHADING
            string hairToSave =
                "alpha_threshold=" + alpha_threshold.Text + "\n" +
                "primary_spec_level=" + primary_spec_level.Text + "\n" +
                "secondary_spec_level=" + secondary_spec_level.Text + "\n" +
                "primary_spec_width=" + primary_spec_width.Text + "\n" +
                "secondary_spec_width=" + secondary_spec_width.Text + "\n" +
                "spec_separation=" + spec_separation.Text + "\n" +
                "glint_intensity=" + glint_intensity.Text + "\n" +
                "glint_width=" + glint_width.Text + "\n" +
                "diffuse_level=" + diffuse_level.Text + "\n" +
                "base_absorption=" + base_absorption.Text + "\n" +
                "absorption_rate=" + absorption_rate.Text + "\n" +
                "ao_absorption=" + ao_absorption.Text + "\n" +
                "scatter_dist_rate=" + scatter_dist_rate.Text + "\n" +
                "occlusion_rate=" + occlusion_rate.Text + "\n" +
                "occlusion_bias=" + occlusion_bias.Text + "\n" +
                "occlusion_ao_infl=" + occlusion_ao_infl.Text + "\n" +
                "specular_occlusion=" + specular_occlusion.Text + "\n" +
                "specular_ao=" + specular_ao.Text + "\n" +
                "sub_strand_frequency=" + sub_strand_frequency.Text + "\n" +
                "sub_strand_spec_shift=" + sub_strand_spec_shift.Text + "\n" +
                "softening_length=" + softening_length.Text + "\n" +
                "softening_normal_bias=" + softening_normal_bias.Text + "\n" +
                "softening_distance_rate=" + softening_distance_rate.Text;

            //Write data - HAIR_SHADING
            File.WriteAllText(pathToHairFile, hairToSave);

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Load
        private void RadiosityEditor_Load(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Set common file paths
            pathToLightingFile = gameDirectory + @"\DATA\RADIOSITY_SETTINGS.TXT";
            pathToSkinFile = gameDirectory + @"\DATA\SKIN_SHADING_SETTINGS.TXT";
            pathToHairFile = gameDirectory + @"\DATA\HAIR_SHADING_SETTINGS.TXT";

            //Split lighting data to array
            string[] lightingDataPrimary = Regex.Split(File.ReadAllText(pathToLightingFile), "=|\n"); 
            string[] lightingData = { "", "", "", "", "", "", "", "", "" };
            int counter = 0;
            int counterMain = 0;
            foreach (string lighting in lightingDataPrimary)
            {
                if (counterMain % 2 != 0)
                {
                    lightingData[counter] = lighting;
                    counter++;
                }
                counterMain++;
            }

            //Split skin data to array
            string[] skinDataPrimary = Regex.Split(File.ReadAllText(pathToSkinFile), "=|\n");
            string[] skinData = { "", "" };
            counter = 0;
            counterMain = 0;
            foreach (string skin in skinDataPrimary)
            {
                if (counterMain % 2 != 0)
                {
                    skinData[counter] = skin;
                    counter++;
                }
                counterMain++;
            }

            //Split hair data to array
            string[] hairDataPrimary = Regex.Split(File.ReadAllText(pathToHairFile), "=|\n");
            string[] hairData = { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" };
            counter = 0;
            counterMain = 0;
            foreach (string hair in hairDataPrimary)
            {
                if (counterMain % 2 != 0)
                {
                    hairData[counter] = hair;
                    counter++;
                }
                counterMain++;
            }

            //Set data - RADIOSITY
            gRadiosityEmissiveSurfaceScale.Text = lightingData[1];
            gRadiosityFirstBounceScale.Text = lightingData[2];
            gRadiosityMultiBounceScale.Text = lightingData[3];
            gRadiosityAlbedoOverbrightAmount.Text = lightingData[4];
            gRadiosityAlbedoSaturationAmount.Text = lightingData[5];
            gRadiositySpecularGlossScale.Text = lightingData[6];
            gDeferredEmissiveSurfaceScale.Text = lightingData[7];
            gDeferredEmissiveSurfaceExponent.Text = lightingData[8];

            //Set data - SKIN_SHADING
            scattering_radius.Text = skinData[0];
            scattering_saturation.Text = skinData[1];

            //Set data - HAIR_SHADING
            alpha_threshold.Text = hairData[0];
            primary_spec_level.Text = hairData[1];
            secondary_spec_level.Text = hairData[2];
            primary_spec_width.Text = hairData[3];
            secondary_spec_width.Text = hairData[4];
            spec_separation.Text = hairData[5];
            glint_intensity.Text = hairData[6];
            glint_width.Text = hairData[7];
            diffuse_level.Text = hairData[8];
            base_absorption.Text = hairData[9];
            absorption_rate.Text = hairData[10];
            ao_absorption.Text = hairData[11];
            scatter_dist_rate.Text = hairData[12];
            occlusion_rate.Text = hairData[13];
            occlusion_bias.Text = hairData[14];
            occlusion_ao_infl.Text = hairData[15];
            specular_occlusion.Text = hairData[16];
            specular_ao.Text = hairData[17];
            sub_strand_frequency.Text = hairData[18];
            sub_strand_spec_shift.Text = hairData[19];
            softening_length.Text = hairData[20];
            softening_normal_bias.Text = hairData[21];
            softening_distance_rate.Text = hairData[22];

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }
    }
}

/*
 * 
 * Created by Matt Filer
 * www.mattfiler.co.uk
 * 
 */

using Alien_Isolation_Mod_Tools;
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
        Directories AlienDirectories = new Directories();

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
            pathToLightingFile = AlienDirectories.GameDirectoryRoot() + @"\DATA\RADIOSITY_SETTINGS.TXT";
            pathToSkinFile = AlienDirectories.GameDirectoryRoot() + @"\DATA\SKIN_SHADING_SETTINGS.TXT";
            pathToHairFile = AlienDirectories.GameDirectoryRoot() + @"\DATA\HAIR_SHADING_SETTINGS.TXT";


            //Compile data - RADIOSITY
            string radiosityToSave = 
                "settings_file_version=1" + Environment.NewLine +
                "gRadiosityEmissiveSurfaceScale=" + gRadiosityEmissiveSurfaceScale.Text + Environment.NewLine +
                "gRadiosityFirstBounceScale=" + gRadiosityFirstBounceScale.Text + Environment.NewLine +
                "gRadiosityMultiBounceScale=" + gRadiosityMultiBounceScale.Text + Environment.NewLine +
                "gRadiosityAlbedoOverbrightAmount=" + gRadiosityAlbedoOverbrightAmount.Text + Environment.NewLine +
                "gRadiosityAlbedoSaturationAmount=" + gRadiosityAlbedoSaturationAmount.Text + Environment.NewLine +
                "gRadiositySpecularGlossScale=" + gRadiositySpecularGlossScale.Text + Environment.NewLine +
                "gDeferredEmissiveSurfaceScale=" + gDeferredEmissiveSurfaceScale.Text + Environment.NewLine +
                "gDeferredEmissiveSurfaceExponent=" + gDeferredEmissiveSurfaceExponent.Text;

            //Write data - RADIOSITY
            File.WriteAllText(pathToLightingFile, radiosityToSave);


            //compile data - SKIN_SHADING
            string skinToSave =
                "scattering_radius=" + scattering_radius.Text + Environment.NewLine +
                "scattering_saturation=" + scattering_saturation.Text;

            //Write data - SKIN_SHADING
            File.WriteAllText(pathToSkinFile, skinToSave);


            //compile data - HAIR_SHADING
            string hairToSave =
                "alpha_threshold=" + alpha_threshold.Text + Environment.NewLine +
                "primary_spec_level=" + primary_spec_level.Text + Environment.NewLine +
                "secondary_spec_level=" + secondary_spec_level.Text + Environment.NewLine +
                "primary_spec_width=" + primary_spec_width.Text + Environment.NewLine +
                "secondary_spec_width=" + secondary_spec_width.Text + Environment.NewLine +
                "spec_separation=" + spec_separation.Text + Environment.NewLine +
                "glint_intensity=" + glint_intensity.Text + Environment.NewLine +
                "glint_width=" + glint_width.Text + Environment.NewLine +
                "diffuse_level=" + diffuse_level.Text + Environment.NewLine +
                "base_absorption=" + base_absorption.Text + Environment.NewLine +
                "absorption_rate=" + absorption_rate.Text + Environment.NewLine +
                "ao_absorption=" + ao_absorption.Text + Environment.NewLine +
                "scatter_dist_rate=" + scatter_dist_rate.Text + Environment.NewLine +
                "occlusion_rate=" + occlusion_rate.Text + Environment.NewLine +
                "occlusion_bias=" + occlusion_bias.Text + Environment.NewLine +
                "occlusion_ao_infl=" + occlusion_ao_infl.Text + Environment.NewLine +
                "specular_occlusion=" + specular_occlusion.Text + Environment.NewLine +
                "specular_ao=" + specular_ao.Text + Environment.NewLine +
                "sub_strand_frequency=" + sub_strand_frequency.Text + Environment.NewLine +
                "sub_strand_spec_shift=" + sub_strand_spec_shift.Text + Environment.NewLine +
                "softening_length=" + softening_length.Text + Environment.NewLine +
                "softening_normal_bias=" + softening_normal_bias.Text + Environment.NewLine +
                "softening_distance_rate=" + softening_distance_rate.Text;

            //Write data - HAIR_SHADING
            File.WriteAllText(pathToHairFile, hairToSave);

            //Done.
            MessageBox.Show("Successfully saved new lighting settings.");

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Load
        private void RadiosityEditor_Load(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Set common file paths
            pathToLightingFile = AlienDirectories.GameDirectoryRoot() + @"\DATA\RADIOSITY_SETTINGS.TXT";
            pathToSkinFile = AlienDirectories.GameDirectoryRoot() + @"\DATA\SKIN_SHADING_SETTINGS.TXT";
            pathToHairFile = AlienDirectories.GameDirectoryRoot() + @"\DATA\HAIR_SHADING_SETTINGS.TXT";

            //Split lighting data to array
            string[] lightingDataPrimary = Regex.Split(File.ReadAllText(pathToLightingFile), "=|\r\n"); 
            string[] lightingData = { "", "", "", "", "", "", "", "", "" };
            int counter = 0;
            int counterMain = 0;
            foreach (string lighting in lightingDataPrimary)
            {
                if (counterMain % 2 != 0)
                {
                    if (lighting != "")
                    {
                        lightingData[counter] = lighting;
                        counter++;
                    }
                }
                counterMain++;
            }

            //Split skin data to array
            string[] skinDataPrimary = Regex.Split(File.ReadAllText(pathToSkinFile), "=|\r\n");
            string[] skinData = { "", "" };
            counter = 0;
            counterMain = 0;
            foreach (string skin in skinDataPrimary)
            {
                if (counterMain % 2 != 0)
                {
                    if (skin != "")
                    {
                        skinData[counter] = skin;
                        counter++;
                    }
                }
                counterMain++;
            }

            //Split hair data to array
            string[] hairDataPrimary = Regex.Split(File.ReadAllText(pathToHairFile), "=|\r\n");
            string[] hairData = { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" };
            counter = 0;
            counterMain = 0;
            foreach (string hair in hairDataPrimary)
            {
                if (counterMain % 2 != 0)
                {
                    if (hair != "")
                    {
                        hairData[counter] = hair;
                        counter++;
                    }
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

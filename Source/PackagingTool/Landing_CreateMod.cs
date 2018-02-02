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
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PackagingTool
{
    public partial class Landing : Form
    {
        Directories AlienDirectories = new Directories();

        public Landing()
        {
            InitializeComponent();
        }

        //On Load
        private void Landing_Load(object sender, EventArgs e)
        {
            //Load fonts
            PrivateFontCollection ModToolFont = new PrivateFontCollection();
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Isolation.ttf");
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Jixellation.ttf");
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Nostromo.ttf");

            //Set fonts & parents
            groupBox1.Parent = ModCreatorBackground;
            groupBox2.Parent = ModCreatorBackground;
            groupBox5.Parent = ModCreatorBackground;
            groupBox6.Parent = ModCreatorBackground;
            startGame.Parent = ModCreatorBackground;
            ModCreatorHeader.Font = new Font(ModToolFont.Families[1], 24);
            ModCreatorHeader.Parent = ModCreatorBackground;
        }

        //Open BehaviourPacker
        private void openBehaviourTreePackager_Click(object sender, EventArgs e)
        {
            BehaviourPacker packerForm = new BehaviourPacker();
            packerForm.Show();
        }

        //Open CharEd
        private void button2_Click(object sender, EventArgs e)
        {
            CharEd attributeForm = new CharEd();
            attributeForm.Show();
        }

        //Open AlienConfigEditor
        private void openAlienConfig_Click(object sender, EventArgs e)
        {
            AlienConfigEditor alienConfigForm = new AlienConfigEditor();
            alienConfigForm.Show();
        }

        //Open DifficultyEditor
        private void openDifficultyEditor_Click(object sender, EventArgs e)
        {
            DifficultyEditor diffEditorForm = new DifficultyEditor();
            diffEditorForm.Show();
        }

        //Open ViewconeEditor (global)
        private void openViewconeEditor_Click(object sender, EventArgs e)
        {
            ViewconeEditor viewconeSetEditor = new ViewconeEditor();
            viewconeSetEditor.Show();
        }

        //Open ViewconeEditor (character)
        private void openCharViewconeEditor_Click(object sender, EventArgs e)
        {
            CharViewconeEditor viewconeCharSetEditor = new CharViewconeEditor();
            viewconeCharSetEditor.Show();
        }

        //Open WeaponEditor
        private void openWeaponEditor_Click(object sender, EventArgs e)
        {
            WeaponEditor weaponEditorForm = new WeaponEditor();
            weaponEditorForm.Show();
        }

        //Open radiosity editor
        private void openRadiosityEditor_Click(object sender, EventArgs e)
        {
            RadiosityEditor radEdFormc = new RadiosityEditor();
            radEdFormc.Show();
        }
        
        private void button7_Click(object sender, EventArgs e)
        {
        }
        
        private void weaponInvSettings_Click(object sender, EventArgs e)
        {
        }

        //open loot inventory settings
        private void openLootInvSettings_Click(object sender, EventArgs e)
        {
            InventoryLoot openInvEditor = new InventoryLoot();
            openInvEditor.Show();
        }

        //open loadscreen movie playlist editor
        private void button4_Click(object sender, EventArgs e)
        {
            LoadMovieEditor openLoadscreenEditor = new LoadMovieEditor();
            openLoadscreenEditor.Show();
        }

        //open blueprint editor
        private void openBlueprintEditor_Click(object sender, EventArgs e)
        {
            BlueprintEditor openBPEditor = new BlueprintEditor();
            openBPEditor.Show();
        }

        //open hack tool editor
        private void openHackEditor_Click(object sender, EventArgs e)
        {
            HackingEditor openHackEditor = new HackingEditor();
            openHackEditor.Show();
        }

        //open locomotion editor
        private void button1_Click(object sender, EventArgs e)
        {
            LocomotionEditor openLocomotionEditor = new LocomotionEditor();
            openLocomotionEditor.Show();
        }

        //open game
        private void startGame_Click(object sender, EventArgs e)
        {
            //Process.Start(gameDirectory + @"\AI.exe");
            Process.Start("steam://rungameid/214490");
        }

        private void doBenchmark_Click(object sender, EventArgs e)
        {
            //unused
        }

        //Close
        private void CloseButton_Click(object sender, EventArgs e)
        {
            try
            {
                Directory.Delete(AlienDirectories.ToolWorkingDirectory(), true);
                Directory.CreateDirectory(AlienDirectories.ToolWorkingDirectory());
            }
            catch { }
            try { GC.Collect(); GC.WaitForPendingFinalizers(); } catch { }
            Landing_Main LandingForm = new Landing_Main();
            LandingForm.Show();
            this.Hide();
        }
    }
}

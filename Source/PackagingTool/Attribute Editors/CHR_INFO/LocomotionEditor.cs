/*
 * 
 * Created by Matt Filer
 * www.mattfiler.co.uk
 * 
 */
 
using PackagingTool;
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
using System.Xml.Linq;
using System.Xml.XPath;

namespace Alien_Isolation_Mod_Tools
{
    public partial class LocomotionEditor : Form
    {
        //Load shared scripts
        AYZ_AttributeEditors AlienAttribute = new AYZ_AttributeEditors();

        //Common file paths
        string pathToWorkingXML;
        string gameBmlDirectory = @"\DATA\CHR_INFO\ATTRIBUTES\";

        public LocomotionEditor()
        {
            InitializeComponent();

            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        //load character data
        private void loadChar_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected class name
            string selectedClass = characters.Text;

            if (selectedClass == "")
            {
                //No class selected, can't load anything
                MessageBox.Show("Please select a character class.");
            }
            else
            {
                //Load in XML
                pathToWorkingXML = AlienAttribute.loadXML(selectedClass, gameBmlDirectory);

                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Load vals
                AlienAttribute.getNode("Locomotion", "permittedLocomotionModulation", ChrAttributeXML, permittedLocomotionModulation, null);
                AlienAttribute.getNode("Locomotion", "capsuleRadius", ChrAttributeXML, capsuleRadius, null);
                AlienAttribute.getNode("Locomotion", "capsuleHeight", ChrAttributeXML, capsuleHeight, null);

                //Enable
                loadSet.Enabled = true;
                AlienAttribute.enableInput(null, setList);

                //Disable
                AlienAttribute.disableInput(linearVelocity1, null);
                AlienAttribute.disableInput(linearAcceleration1, null);
                AlienAttribute.disableInput(maxAngularVelocity1, null);
                AlienAttribute.disableInput(angularAcceleration1, null);
                AlienAttribute.disableInput(stoppingDistance1, null);
                AlienAttribute.disableInput(corneringWeight1, null);
                AlienAttribute.disableInput(corneringPenalty1, null);
                AlienAttribute.disableInput(maxAngularWarping1, null);
                AlienAttribute.disableInput(maxLinearWarping1, null);
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //load set from character
        private void loadSet_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            if (setList.Text == "")
            {
                //No config selected, can't load anything
                MessageBox.Show("Please select a set.");
            }
            else
            {
                //Enable options
                swapVariant.Enabled = true;
                AlienAttribute.enableInput(null, variantType);

                //Disable
                AlienAttribute.disableInput(linearVelocity1, null);
                AlienAttribute.disableInput(linearAcceleration1, null);
                AlienAttribute.disableInput(maxAngularVelocity1, null);
                AlienAttribute.disableInput(angularAcceleration1, null);
                AlienAttribute.disableInput(stoppingDistance1, null);
                AlienAttribute.disableInput(corneringWeight1, null);
                AlienAttribute.disableInput(corneringPenalty1, null);
                AlienAttribute.disableInput(maxAngularWarping1, null);
                AlienAttribute.disableInput(maxLinearWarping1, null);
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Swap variant
        private void swapVariant_Click(object sender, EventArgs e)
        {
            //Load-in the correct locomotion data
            loadLocomotionData(variantType.SelectedIndex);
        }

        //save
        private void btnSave_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            if (variantType.Text == "" && linearVelocity1.Text =="")
            {
                //No config selected, can't load anything
                MessageBox.Show("Please select a variant first.");
            }
            else
            {
                saveLocomotionData(variantType.SelectedIndex);
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Load locomotion set
        void loadLocomotionData(int type)
        {
            //Load-in XML data
            var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

            //Get correct XML node path
            string locomotionPath = "steeringBoundaryData";
            switch (type)
            {
                case 1:
                    locomotionPath = "crouchedSteeringBoundaryData";
                    break;
                case 2:
                    locomotionPath = "aimedSteeringBoundaryData";
                    break;
            }

            //Load correct set
            AlienAttribute.getNodeAttribute("(//SteeringControls/" + locomotionPath + "/ steeringBoundary)[" + setList.Text + "]", "linearVelocity", ChrAttributeXML, linearVelocity1, null);
            AlienAttribute.getNodeAttribute("(//SteeringControls/" + locomotionPath + "/steeringBoundary)[" + setList.Text + "]", "linearAcceleration", ChrAttributeXML, linearAcceleration1, null);
            AlienAttribute.getNodeAttribute("(//SteeringControls/" + locomotionPath + "/steeringBoundary)[" + setList.Text + "]", "maxAngularVelocity", ChrAttributeXML, maxAngularVelocity1, null);
            AlienAttribute.getNodeAttribute("(//SteeringControls/" + locomotionPath + "/steeringBoundary)[" + setList.Text + "]", "angularAcceleration", ChrAttributeXML, angularAcceleration1, null);
            AlienAttribute.getNodeAttribute("(//SteeringControls/" + locomotionPath + "/steeringBoundary)[" + setList.Text + "]", "stoppingDistance", ChrAttributeXML, stoppingDistance1, null);
            AlienAttribute.getNodeAttribute("(//SteeringControls/" + locomotionPath + "/steeringBoundary)[" + setList.Text + "]", "corneringWeight", ChrAttributeXML, corneringWeight1, null);
            AlienAttribute.getNodeAttribute("(//SteeringControls/" + locomotionPath + "/steeringBoundary)[" + setList.Text + "]", "corneringPenalty", ChrAttributeXML, corneringPenalty1, null);
            AlienAttribute.getNodeAttribute("(//SteeringControls/" + locomotionPath + "/steeringBoundary)[" + setList.Text + "]", "maxAngularWarping", ChrAttributeXML, maxAngularWarping1, null);
            AlienAttribute.getNodeAttribute("(//SteeringControls/" + locomotionPath + "/steeringBoundary)[" + setList.Text + "]", "maxLinearWarping", ChrAttributeXML, maxLinearWarping1, null);
        }

        //Save locomotion set
        void saveLocomotionData(int type)
        {
            //Load-in XML data
            var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

            //Get correct XML node path
            string locomotionPath = "steeringBoundaryData";
            switch (type)
            {
                case 1:
                    locomotionPath = "crouchedSteeringBoundaryData";
                    break;
                case 2:
                    locomotionPath = "aimedSteeringBoundaryData";
                    break;
            }
            
            //Set specific locomotion values
            AlienAttribute.setNodeAttribute("(//SteeringControls/" + locomotionPath + "/steeringBoundary)[" + setList.Text + "]", "linearVelocity", ChrAttributeXML, linearVelocity1, null);
            AlienAttribute.setNodeAttribute("(//SteeringControls/" + locomotionPath + "/steeringBoundary)[" + setList.Text + "]", "linearAcceleration", ChrAttributeXML, linearAcceleration1, null);
            AlienAttribute.setNodeAttribute("(//SteeringControls/" + locomotionPath + "/steeringBoundary)[" + setList.Text + "]", "maxAngularVelocity", ChrAttributeXML, maxAngularVelocity1, null);
            AlienAttribute.setNodeAttribute("(//SteeringControls/" + locomotionPath + "/steeringBoundary)[" + setList.Text + "]", "angularAcceleration", ChrAttributeXML, angularAcceleration1, null);
            AlienAttribute.setNodeAttribute("(//SteeringControls/" + locomotionPath + "/steeringBoundary)[" + setList.Text + "]", "stoppingDistance", ChrAttributeXML, stoppingDistance1, null);
            AlienAttribute.setNodeAttribute("(//SteeringControls/" + locomotionPath + "/steeringBoundary)[" + setList.Text + "]", "corneringWeight", ChrAttributeXML, corneringWeight1, null);
            AlienAttribute.setNodeAttribute("(//SteeringControls/" + locomotionPath + "/steeringBoundary)[" + setList.Text + "]", "corneringPenalty", ChrAttributeXML, corneringPenalty1, null);
            AlienAttribute.setNodeAttribute("(//SteeringControls/" + locomotionPath + "/steeringBoundary)[" + setList.Text + "]", "maxAngularWarping", ChrAttributeXML, maxAngularWarping1, null);
            AlienAttribute.setNodeAttribute("(//SteeringControls/" + locomotionPath + "/steeringBoundary)[" + setList.Text + "]", "maxLinearWarping", ChrAttributeXML, maxLinearWarping1, null);

            //Set more general values
            AlienAttribute.setNode("Locomotion", "permittedLocomotionModulation", ChrAttributeXML, permittedLocomotionModulation, null);
            AlienAttribute.setNode("Locomotion", "capsuleRadius", ChrAttributeXML, capsuleRadius, null);
            AlienAttribute.setNode("Locomotion", "capsuleHeight", ChrAttributeXML, capsuleHeight, null);

            //Save values
            if (AlienAttribute.saveXML(characters.Text, gameBmlDirectory, ChrAttributeXML))
            {
                MessageBox.Show("Saved new locomotion settings for loaded variant.");
            }
            else
            {
                MessageBox.Show("An error occured while saving.");
            }
        }
    }
}

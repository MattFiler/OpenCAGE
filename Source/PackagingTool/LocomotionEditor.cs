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
                pathToWorkingXML = AlienAttribute.convertCharacterBML(selectedClass);

                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Load vals
                AlienAttribute.getNode("Locomotion", "permittedLocomotionModulation", ChrAttributeXML, permittedLocomotionModulation, null);
                AlienAttribute.getNode("Locomotion", "capsuleRadius", ChrAttributeXML, capsuleRadius, null);
                AlienAttribute.getNode("Locomotion", "capsuleHeight", ChrAttributeXML, capsuleHeight, null);

                //Enable
                loadSet.Enabled = true;
                AlienAttribute.enableInput(null, setList);
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //load set from character
        private void loadSet_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected config name
            string locomotionSet = setList.Text;

            if (locomotionSet == "")
            {
                //No config selected, can't load anything
                MessageBox.Show("Please select a set.");
            }
            else
            {
                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Set locomotion values (3 each)
                AlienAttribute.getNodeAttribute("(//SteeringControls/steeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "linearVelocity", ChrAttributeXML, linearVelocity1, null);
                AlienAttribute.getNodeAttribute("(//SteeringControls/aimedSteeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "linearVelocity", ChrAttributeXML, linearVelocity2, null);
                AlienAttribute.getNodeAttribute("(//SteeringControls/crouchedSteeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "linearVelocity", ChrAttributeXML, linearVelocity3, null);

                AlienAttribute.getNodeAttribute("(//SteeringControls/steeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "linearAcceleration", ChrAttributeXML, linearAcceleration1, null);
                AlienAttribute.getNodeAttribute("(//SteeringControls/aimedSteeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "linearAcceleration", ChrAttributeXML, linearAcceleration2, null);
                AlienAttribute.getNodeAttribute("(//SteeringControls/crouchedSteeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "linearAcceleration", ChrAttributeXML, linearAcceleration3, null);

                AlienAttribute.getNodeAttribute("(//SteeringControls/steeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "maxAngularVelocity", ChrAttributeXML, maxAngularVelocity1, null);
                AlienAttribute.getNodeAttribute("(//SteeringControls/aimedSteeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "maxAngularVelocity", ChrAttributeXML, maxAngularVelocity2, null);
                AlienAttribute.getNodeAttribute("(//SteeringControls/crouchedSteeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "maxAngularVelocity", ChrAttributeXML, maxAngularVelocity3, null);

                AlienAttribute.getNodeAttribute("(//SteeringControls/steeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "angularAcceleration", ChrAttributeXML, angularAcceleration1, null);
                AlienAttribute.getNodeAttribute("(//SteeringControls/aimedSteeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "angularAcceleration", ChrAttributeXML, angularAcceleration2, null);
                AlienAttribute.getNodeAttribute("(//SteeringControls/crouchedSteeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "angularAcceleration", ChrAttributeXML, angularAcceleration3, null);

                AlienAttribute.getNodeAttribute("(//SteeringControls/steeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "stoppingDistance", ChrAttributeXML, stoppingDistance1, null);
                AlienAttribute.getNodeAttribute("(//SteeringControls/aimedSteeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "stoppingDistance", ChrAttributeXML, stoppingDistance2, null);
                AlienAttribute.getNodeAttribute("(//SteeringControls/crouchedSteeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "stoppingDistance", ChrAttributeXML, stoppingDistance3, null);

                AlienAttribute.getNodeAttribute("(//SteeringControls/steeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "corneringWeight", ChrAttributeXML, corneringWeight1, null);
                AlienAttribute.getNodeAttribute("(//SteeringControls/aimedSteeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "corneringWeight", ChrAttributeXML, corneringWeight2, null);
                AlienAttribute.getNodeAttribute("(//SteeringControls/crouchedSteeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "corneringWeight", ChrAttributeXML, corneringWeight3, null);

                AlienAttribute.getNodeAttribute("(//SteeringControls/steeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "corneringPenalty", ChrAttributeXML, corneringPenalty1, null);
                AlienAttribute.getNodeAttribute("(//SteeringControls/aimedSteeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "corneringPenalty", ChrAttributeXML, corneringPenalty2, null);
                AlienAttribute.getNodeAttribute("(//SteeringControls/crouchedSteeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "corneringPenalty", ChrAttributeXML, corneringPenalty3, null);

                AlienAttribute.getNodeAttribute("(//SteeringControls/steeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "maxAngularWarping", ChrAttributeXML, maxAngularWarping1, null);
                AlienAttribute.getNodeAttribute("(//SteeringControls/aimedSteeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "maxAngularWarping", ChrAttributeXML, maxAngularWarping2, null);
                AlienAttribute.getNodeAttribute("(//SteeringControls/crouchedSteeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "maxAngularWarping", ChrAttributeXML, maxAngularWarping3, null);

                AlienAttribute.getNodeAttribute("(//SteeringControls/steeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "maxLinearWarping", ChrAttributeXML, maxLinearWarping1, null);
                AlienAttribute.getNodeAttribute("(//SteeringControls/aimedSteeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "maxLinearWarping", ChrAttributeXML, maxLinearWarping2, null);
                AlienAttribute.getNodeAttribute("(//SteeringControls/crouchedSteeringBoundaryData/steeringBoundary)[" + locomotionSet + "]", "maxLinearWarping", ChrAttributeXML, maxLinearWarping3, null);
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //save
        private void btnSave_Click(object sender, EventArgs e)
        {

        }





        



        

        //Set XML value
        private void saveAttributeValue(string attributeGroup, string specificParameter, XDocument ChrAttributeXML, TextBox textboxToSet, ComboBox comboboxToSet)
        {
            try
            {
                ChrAttributeXML.XPathSelectElement("//" + attributeGroup).Attribute(specificParameter).Value = textboxToSet.Text;
            }
            catch
            {
                //Can't save, hopefully because doesnt exist (should be).
            }
        }
    }
}

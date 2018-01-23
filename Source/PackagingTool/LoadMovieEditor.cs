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
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Alien_Isolation_Mod_Tools
{
    public partial class LoadMovieEditor : Form
    {
        //Main Directories
        string workingDirectory = Directory.GetCurrentDirectory() + @"\Attribute Editor Directory\"; //Our working dir
        string gameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz"); //Our game's dir

        //Common file paths
        string pathToWorkingBML;
        string pathToGameBML;
        string pathToGameXML;
        string pathToWorkingXML;
    
        public LoadMovieEditor()
        {
            InitializeComponent();

            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;

            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Set common file paths
            pathToWorkingBML = workingDirectory + "GBL_ITEM.BML";
            pathToGameBML = gameDirectory + @"\DATA\GBL_ITEM.BML";
            pathToGameXML = gameDirectory + @"\DATA\GBL_ITEM.XML";
            pathToWorkingXML = workingDirectory + "GBL_ITEM.xml";

            //Copy correct XML to working directory and fix bug
            StreamWriter updateXmlContents = new StreamWriter(pathToWorkingXML);
            updateXmlContents.WriteLine(File.ReadAllText(pathToGameXML).Replace(" xmlns=\"http://www.w3schools.com\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://www.w3schools.com gbl_item.xsd\"", ""));
            updateXmlContents.Close();


            //Load-in XML data
            var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

            //Get playlists
            moviePlaylists.Items.Clear();
            IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//item_database/movie_playlists/movie_playlist");
            foreach (XElement el in elements)
            {
                moviePlaylists.Items.Add(el.Attribute("playlist_name").Value.ToString());
                moviePlaylists.Enabled = true;
                btnSelectClass.Enabled = true;
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Load
        private void btnSelectClass_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected playlist name
            string selectedType = moviePlaylists.Text;

            if (selectedType == "")
            {
                //No playlist selected, can't load anything
                MessageBox.Show("Please select a movie playlist first.");
            }
            else
            {
                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Get all data from type
                IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//item_database/movie_playlists/movie_playlist");
                foreach (XElement el in elements)
                {
                    if (el.Attribute("playlist_name").Value.ToString() == selectedType)
                    {
                        terminate_on_load_completed.Enabled = true;
                        terminate_on_load_completed.Text = el.Attribute("terminate_on_load_completed").Value.ToString();
                        allow_player_to_skip.Enabled = true;
                        allow_player_to_skip.Text = el.Attribute("allow_player_to_skip").Value.ToString();
                        shuffle_playlist.Enabled = true;
                        shuffle_playlist.Text = el.Attribute("shuffle_playlist").Value.ToString();
                        loop_playlist.Enabled = true;
                        loop_playlist.Text = el.Attribute("loop_playlist").Value.ToString();

                        movieList.Items.Clear();
                        movieList.Enabled = true;

                        addMovie.Enabled = true;
                        removeMovie.Enabled = true;

                        //Get XML Comment
                        string devComments = "";
                        foreach (XNode comment in ChrAttributeXML.Elements().DescendantNodesAndSelf())
                        {
                            if (comment.NodeType == XmlNodeType.Comment && comment.NextNode.ToString() == el.ToString())
                            {
                                try
                                {
                                    if (comment.PreviousNode.NodeType == XmlNodeType.Comment)
                                    {
                                        devComments = devComments + comment.PreviousNode.ToString().Substring(4, comment.PreviousNode.ToString().Length - 7) + Environment.NewLine;
                                    }
                                }
                                catch
                                {
                                    //no previous node
                                }
                                devComments = devComments + comment.ToString().Substring(4, comment.ToString().Length - 7) + Environment.NewLine;
                            }
                        }
                        dev_comments.Text = devComments;

                        IEnumerable<XElement> clipElements = el.XPathSelectElements("clip");
                        foreach (XElement clip in clipElements)
                        {
                            movieList.Items.Add(clip.Attribute("clip_name").Value.ToString());
                        }
                    }
                }
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Save
        private void btnSave_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected playlist name
            string selectedType = moviePlaylists.Text;

            if (selectedType == "")
            {
                //No playlist selected, can't load anything
                MessageBox.Show("Please select a movie playlist first.");
            }
            else
            {
                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Get all data from type
                IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//item_database/movie_playlists/movie_playlist");
                foreach (XElement el in elements)
                {
                    if (el.Attribute("playlist_name").Value.ToString() == selectedType)
                    {
                        el.Attribute("terminate_on_load_completed").Value = terminate_on_load_completed.Text;
                        el.Attribute("allow_player_to_skip").Value = allow_player_to_skip.Text;
                        el.Attribute("shuffle_playlist").Value = shuffle_playlist.Text;
                        el.Attribute("loop_playlist").Value = loop_playlist.Text;
                        
                        //remove original clip list
                        el.RemoveNodes();

                        //Compile new clip list
                        int itemCounter = 0;
                        foreach (string movie in movieList.Items)
                        {
                            el.Add(XElement.Parse("<clip clip_name=\"" + movie + "\" clip_index=\"" + itemCounter + "\" />"));
                            itemCounter++;
                        }
                    }
                }
                
                //Save all to XML
                ChrAttributeXML.Save(pathToWorkingXML);

                //Convert XML to BML
                new AlienConverter(pathToWorkingXML, pathToWorkingBML).Run();

                //Copy new BML to game directory & remove working files
                File.Delete(pathToGameBML);
                File.Copy(pathToWorkingBML, pathToGameBML);
                File.Delete(pathToGameXML);
                File.Copy(pathToWorkingXML, pathToGameXML);
                File.Delete(pathToWorkingBML);
                //File.Delete(pathToWorkingXML);

                //Done
                MessageBox.Show("Saved new movie playlist settings.");
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Add new movie to listbox
        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog selectGameFile = new OpenFileDialog();
            selectGameFile.InitialDirectory = gameDirectory + @"\DATA\UI\MOVIES\";
            if (selectGameFile.ShowDialog() == DialogResult.OK &&
                selectGameFile.FileName != "")
            {
                movieList.Items.Add("Movies/" + Path.GetFileName(selectGameFile.FileName));
            }
        }

        //Remove movie from listbox
        private void removeMovie_Click(object sender, EventArgs e)
        {
            try
            {
                movieList.Items.RemoveAt(movieList.SelectedIndex);
            }
            catch
            {
                MessageBox.Show("Please select a movie to remove.");
            }
        }
    }
}

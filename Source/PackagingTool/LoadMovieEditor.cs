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

                        movie_1.Enabled = false;
                        movie_2.Enabled = false;
                        movie_3.Enabled = false;
                        movie_4.Enabled = false;
                        movie_5.Enabled = false;
                        movie_6.Enabled = false;
                        movie_7.Enabled = false;
                        movie_8.Enabled = false;
                        movie_9.Enabled = false;
                        movie_10.Enabled = false;

                        movie_1.Text = "";
                        movie_2.Text = "";
                        movie_3.Text = "";
                        movie_4.Text = "";
                        movie_5.Text = "";
                        movie_6.Text = "";
                        movie_7.Text = "";
                        movie_8.Text = "";
                        movie_9.Text = "";
                        movie_10.Text = "";

                        movie_select_1.Enabled = false;
                        movie_select_2.Enabled = false;
                        movie_select_3.Enabled = false;
                        movie_select_4.Enabled = false;
                        movie_select_5.Enabled = false;
                        movie_select_6.Enabled = false;
                        movie_select_7.Enabled = false;
                        movie_select_8.Enabled = false;
                        movie_select_9.Enabled = false;
                        movie_select_10.Enabled = false;

                        //Get XML Comment
                        string devComments = "";
                        foreach (XNode comment in ChrAttributeXML.Elements().DescendantNodesAndSelf())
                        {
                            if (comment.NodeType == XmlNodeType.Comment && comment.NextNode.ToString() == el.ToString())
                            {
                                if (comment.PreviousNode.NodeType == XmlNodeType.Comment)
                                {
                                    devComments = devComments + comment.PreviousNode.ToString().Substring(4, comment.PreviousNode.ToString().Length - 7) + Environment.NewLine;
                                }
                                devComments = devComments + comment.ToString().Substring(4, comment.ToString().Length - 7) + Environment.NewLine;
                            }
                        }
                        dev_comments.Text = devComments;

                        IEnumerable<XElement> clipElements = el.XPathSelectElements("clip");
                        int loopcount = 0;
                        foreach (XElement clip in clipElements)
                        {
                            loopcount++;
                            switch (loopcount)
                            {
                                case 1:
                                    movie_1.Text = clip.Attribute("clip_name").Value.ToString();
                                    movie_1.Enabled = true;
                                    movie_select_1.Enabled = true;
                                    break;
                                case 2:
                                    movie_2.Text = clip.Attribute("clip_name").Value.ToString();
                                    movie_2.Enabled = true;
                                    movie_select_2.Enabled = true;
                                    break;
                                case 3:
                                    movie_3.Text = clip.Attribute("clip_name").Value.ToString();
                                    movie_3.Enabled = true;
                                    movie_select_3.Enabled = true;
                                    break;
                                case 4:
                                    movie_4.Text = clip.Attribute("clip_name").Value.ToString();
                                    movie_4.Enabled = true;
                                    movie_select_4.Enabled = true;
                                    break;
                                case 5:
                                    movie_5.Text = clip.Attribute("clip_name").Value.ToString();
                                    movie_5.Enabled = true;
                                    movie_select_5.Enabled = true;
                                    break;
                                case 6:
                                    movie_6.Text = clip.Attribute("clip_name").Value.ToString();
                                    movie_6.Enabled = true;
                                    movie_select_6.Enabled = true;
                                    break;
                                case 7:
                                    movie_7.Text = clip.Attribute("clip_name").Value.ToString();
                                    movie_7.Enabled = true;
                                    movie_select_7.Enabled = true;
                                    break;
                                case 8:
                                    movie_8.Text = clip.Attribute("clip_name").Value.ToString();
                                    movie_8.Enabled = true;
                                    movie_select_8.Enabled = true;
                                    break;
                                case 9:
                                    movie_9.Text = clip.Attribute("clip_name").Value.ToString();
                                    movie_9.Enabled = true;
                                    movie_select_9.Enabled = true;
                                    break;
                                case 10:
                                    movie_10.Text = clip.Attribute("clip_name").Value.ToString();
                                    movie_10.Enabled = true;
                                    movie_select_10.Enabled = true;
                                    break;
                            }
                        }
                    }
                }
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //movie load 1
        private void movie_select_1_Click(object sender, EventArgs e)
        {
            loadMovie(movie_1);
        }

        //movie load 2
        private void movie_select_2_Click(object sender, EventArgs e)
        {
            loadMovie(movie_2);
        }

        //movie load 3
        private void movie_select_3_Click(object sender, EventArgs e)
        {
            loadMovie(movie_3);
        }

        //movie load 4
        private void movie_select_4_Click(object sender, EventArgs e)
        {
            loadMovie(movie_4);
        }

        //movie load 5
        private void movie_select_5_Click(object sender, EventArgs e)
        {
            loadMovie(movie_5);
        }

        //movie load 6
        private void movie_select_6_Click(object sender, EventArgs e)
        {
            loadMovie(movie_6);
        }

        //movie load 7
        private void movie_select_7_Click(object sender, EventArgs e)
        {
            loadMovie(movie_7);
        }

        //movie load 8
        private void movie_select_8_Click(object sender, EventArgs e)
        {
            loadMovie(movie_8);
        }

        //movie load 9
        private void movie_select_9_Click(object sender, EventArgs e)
        {
            loadMovie(movie_9);
        }

        //movie load 10
        private void movie_select_10_Click(object sender, EventArgs e)
        {
            loadMovie(movie_10);
        }

        private void loadMovie(TextBox textbox)
        {
            OpenFileDialog selectGameFile = new OpenFileDialog();
            selectGameFile.InitialDirectory = gameDirectory + @"\DATA\UI\MOVIES\";
            if (selectGameFile.ShowDialog() == DialogResult.OK)
            {
                textbox.Text = "Movies/" + Path.GetFileName(selectGameFile.FileName);
            }
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

                        IEnumerable<XElement> clipElements = el.XPathSelectElements("clip");
                        int loopcount = 0;
                        foreach (XElement clip in clipElements)
                        {
                            loopcount++;
                            switch (loopcount)
                            {
                                case 1:
                                    clip.Attribute("clip_name").Value = movie_1.Text;
                                    break;
                                case 2:
                                    clip.Attribute("clip_name").Value = movie_2.Text;
                                    break;
                                case 3:
                                    clip.Attribute("clip_name").Value = movie_3.Text;
                                    break;
                                case 4:
                                    clip.Attribute("clip_name").Value = movie_4.Text;
                                    break;
                                case 5:
                                    clip.Attribute("clip_name").Value = movie_5.Text;
                                    break;
                                case 6:
                                    clip.Attribute("clip_name").Value = movie_6.Text;
                                    break;
                                case 7:
                                    clip.Attribute("clip_name").Value = movie_7.Text;
                                    break;
                                case 8:
                                    clip.Attribute("clip_name").Value = movie_8.Text;
                                    break;
                                case 9:
                                    clip.Attribute("clip_name").Value = movie_9.Text;
                                    break;
                                case 10:
                                    clip.Attribute("clip_name").Value = movie_10.Text;
                                    break;
                            }
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
    }
}

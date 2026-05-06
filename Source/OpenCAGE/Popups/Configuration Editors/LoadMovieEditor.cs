using CATHODE;
using CATHODE.Animations;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.ConfigEditors
{
    public partial class LoadMovieEditor : BaseWindow
    {
        private readonly BML _gblItem;

        public LoadMovieEditor() : base()
        {
            InitializeComponent();

            _gblItem = new BML(Singleton.PathToAI + @"\DATA\GBL_ITEM.BML");

            var playlists = _gblItem.Content["item_database"]["movie_playlists"];
            foreach (XmlElement playlist in playlists)
            {
                moviePlaylists.Items.Add(playlist.GetAttribute("playlist_name"));
            }
            moviePlaylists.Enabled = playlists.ChildNodes.Count != 0;
            if (moviePlaylists.Enabled)
                moviePlaylists.SelectedIndex = 0;

            Singleton.OnResetConfigs += () => { this.Close(); };
        }

        private void moviePlaylists_SelectedIndexChanged(object sender, EventArgs e)
        {
            var playlists = _gblItem.Content["item_database"]["movie_playlists"];
            foreach (XmlElement playlist in playlists)
            {
                if (playlist.GetAttribute("playlist_name") != moviePlaylists.Text)
                    continue;

                endWhenLoaded.Checked = playlist.GetAttribute("terminate_on_load_completed").ToLower() == "true";
                allowSkip.Checked = playlist.GetAttribute("allow_player_to_skip").ToLower() == "true";
                shuffle.Checked = playlist.GetAttribute("shuffle_playlist").ToLower() == "true";
                loop.Checked = playlist.GetAttribute("loop_playlist").ToLower() == "true";

                movieList.BeginUpdate();
                movieList.Items.Clear();
                foreach (XmlElement clip in playlist)
                    movieList.Items.Add(clip.GetAttribute("clip_name"));
                movieList.EndUpdate();
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            var doc = _gblItem.Content;

            var playlists = doc["item_database"]["movie_playlists"];
            foreach (XmlElement playlist in playlists)
            {
                if (playlist.GetAttribute("playlist_name") != moviePlaylists.Text)
                    continue;

                playlist.RemoveAll();

                playlist.SetAttribute("playlist_name", moviePlaylists.Text);
                playlist.SetAttribute("terminate_on_load_completed", endWhenLoaded.Checked.ToString().ToLower());
                playlist.SetAttribute("allow_player_to_skip", allowSkip.Checked.ToString().ToLower());
                playlist.SetAttribute("shuffle_playlist", shuffle.Checked.ToString().ToLower());
                playlist.SetAttribute("loop_playlist", loop.Checked.ToString().ToLower());

                for (int i = 0; i < movieList.Items.Count; i++)
                {
                    XmlElement clip = doc.CreateElement("clip");
                    clip.SetAttribute("clip_name", movieList.Items[i].Text);
                    clip.SetAttribute("clip_index", i.ToString());
                    playlist.AppendChild(clip);
                }
            }

            _gblItem.Content = doc;
            _gblItem.Save();

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void addMovie_Click(object sender, EventArgs e)
        {
            MovieSelectorPopup popup = new MovieSelectorPopup();
            popup.Show();
            popup.OnMovieSelected += OnMovieSelected;
        }
        private void OnMovieSelected(string movie)
        {
            movieList.Items.Add("Movies/" + movie);
        }

        private void removeMovie_Click(object sender, EventArgs e)
        {
            if (movieList.SelectedItems.Count == 0)
                return;

            movieList.Items.Remove(movieList.SelectedItems[0]);
        }

        private void moveMovieDown_Click(object sender, EventArgs e)
        {
            if (movieList.SelectedItems.Count == 0)
                return;

            ListViewItem item = movieList.SelectedItems[0];
            if (item.Index == movieList.Items.Count - 1) return;
            int index = item.Index + 1;

            movieList.Items.Remove(item);
            movieList.Items.Insert(index, item);
        }

        private void moveMovieUp_Click(object sender, EventArgs e)
        {
            if (movieList.SelectedItems.Count == 0)
                return;

            ListViewItem item = movieList.SelectedItems[0];
            if (item.Index == 0) return;
            int index = item.Index - 1;

            movieList.Items.Remove(item);
            movieList.Items.Insert(index, item);
        }

        private void helpBtn_Click(object sender, EventArgs e)
        {
            Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
            Process.Start("https://opencage.co.uk/docs/configs/loadscreen-movies");
        }
    }
}

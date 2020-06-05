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
using AlienPAK;
using Newtonsoft.Json.Linq;
using NAudio.Wave;
using System.Threading;

namespace Alien_Isolation_Mod_Tools
{
    public partial class SoundTool : Form
    {
        List<SoundFile> SoundFiles = new List<SoundFile>();
        ToolPaths Paths = new ToolPaths();
        ContentTools_Loadscreen loadscreen;

        public SoundTool()
        {
            InitializeComponent();

            loadscreen = new ContentTools_Loadscreen(null, this);
            loadscreen.Show();
        }

        /* Start loading content when loadscreen is visible */
        public void StartLoadingContent()
        {
            /*
            JObject soundFilesJSON = JObject.Parse(LocalAsset.GetAsString("Sound Resources", "soundbank.json"));
            foreach (JObject sound in soundFilesJSON["soundbank_names"])
            {
                SoundFile newSound = new SoundFile();
                newSound.FileName = sound["new_name"].Value<string>();
                newSound.OriginalID = sound["original_id"].Value<int>();
                SoundFiles.Add(newSound);
            }

            FileTree.Nodes.Clear();
            foreach (SoundFile SoundFile in SoundFiles)
            {
                string[] FileNameParts = SoundFile.FileName.Split('/');
                if (FileNameParts.Length == 1) { FileNameParts = SoundFile.FileName.Split('\\'); }
                AddFileToTree(FileNameParts, 0, FileTree.Nodes);
            }
            FileTree.Sort(); //TODO: do this sort offline
            */
            loadscreen.Close();
        }
        
        /* Add a file to the GUI tree structure (PORTED FROM ALIENPAK) */
        private void AddFileToTree(string[] FileNameParts, int index, TreeNodeCollection LoopedNodeCollection)
        {
            if (FileNameParts.Length <= index)
            {
                return;
            }

            bool should = true;
            foreach (TreeNode ThisFileNode in LoopedNodeCollection)
            {
                if (ThisFileNode.Text == FileNameParts[index])
                {
                    should = false;
                    AddFileToTree(FileNameParts, index + 1, ThisFileNode.Nodes);
                    break;
                }
            }
            if (should)
            {
                TreeNode FileNode = new TreeNode(FileNameParts[index]);
                TreeItem ThisTag = new TreeItem();
                if (FileNameParts.Length - 1 == index)
                {
                    //Node is a file
                    for (int i = 0; i < FileNameParts.Length; i++)
                    {
                        ThisTag.String_Value += FileNameParts[i] + "/";
                    }
                    ThisTag.String_Value = ThisTag.String_Value.ToString().Substring(0, ThisTag.String_Value.ToString().Length - 1);

                    ThisTag.Item_Type = TreeItemType.EXPORTABLE_FILE;
                    FileNode.ImageIndex = (int)TreeItemIcon.FILE;
                    FileNode.SelectedImageIndex = (int)TreeItemIcon.FILE;
                    FileNode.ContextMenuStrip = fileContextMenu;
                }
                else
                {
                    //Node is a directory
                    ThisTag.Item_Type = TreeItemType.DIRECTORY;
                    FileNode.ImageIndex = (int)TreeItemIcon.FOLDER;
                    FileNode.SelectedImageIndex = (int)TreeItemIcon.FOLDER;
                    AddFileToTree(FileNameParts, index + 1, FileNode.Nodes);
                }

                FileNode.Tag = ThisTag;
                LoopedNodeCollection.Add(FileNode);
            }
        }

        private void FileTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            //pull data from the file location listed
        }

        WaveOutEvent soundPlayer = new WaveOutEvent();
        private void PlaySound_Click(object sender, EventArgs _e)
        {
            if (soundPlayer.PlaybackState == PlaybackState.Playing) return;
            Stream fs = File.OpenRead(@"D:\Steam Library\steamapps\common\Alien Isolation\DATA\SOUND_ORGANISED\Voices\English(US)\A1_CV1_AlienDeath_01.ogg");
            VorbisWaveStream waveStream = new VorbisWaveStream(fs);
            soundPlayer.Init(waveStream);
            soundPlayer.Play();
            //todo: add threaded progress bar
        }
        private void StopSound_Click(object sender, EventArgs e)
        {
            if (soundPlayer.PlaybackState == PlaybackState.Playing) soundPlayer.Stop();
        }
    }

    public class SoundFile
    {
        public string FileName = "";
        public int OriginalID = 0;
    }
}

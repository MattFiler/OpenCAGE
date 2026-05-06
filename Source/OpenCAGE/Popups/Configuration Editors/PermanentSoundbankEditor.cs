using CommandsEditor.Popups.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CommandsEditor.ConfigEditors
{
    public partial class PermanentSoundbankEditor : BaseWindow
    {
        public PermanentSoundbankEditor() : base()
        {
            InitializeComponent();

            string[] soundbanks = File.ReadAllLines(Singleton.PathToAI + @"\DATA\LIST_OF_PERMANENT_SOUND_BANKS.TXT");
            permaSoundbanks.BeginUpdate();
            foreach (string soundbank in soundbanks)
            {
                permaSoundbanks.Items.Add(soundbank);
            }
            permaSoundbanks.EndUpdate();

            Singleton.OnResetConfigs += () => { this.Close(); };
        }

        private void addNew_Click(object sender, EventArgs e)
        {
            PermaSoundbankPopup popup = new PermaSoundbankPopup();
            popup.Show();
            popup.OnSoundbankAdded += OnSoundbankAdded;
        }
        private void OnSoundbankAdded(string soundbank)
        {
            permaSoundbanks.Items.Add(soundbank);
            Save();
        }

        private void removeSelected_Click(object sender, EventArgs e)
        {
            if (permaSoundbanks.SelectedIndex == -1)
                return;

            permaSoundbanks.Items.RemoveAt(permaSoundbanks.SelectedIndex);
            Save();
        }

        private void Save()
        {
            List<string> soundbanks = new List<string>();
            for (int i = 0; i < permaSoundbanks.Items.Count; i++)
            {
                soundbanks.Add(permaSoundbanks.Items[i].ToString());
            }
            File.WriteAllLines(Singleton.PathToAI + @"\DATA\LIST_OF_PERMANENT_SOUND_BANKS.TXT", soundbanks);

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void helpBtn_Click(object sender, EventArgs e)
        {
            Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
            Process.Start("https://opencage.co.uk/docs/configs/perma-soundbanks");
        }
    }
}

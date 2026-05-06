using CATHODE;
using CommandsEditor.Popups.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using WeifenLuo.WinFormsUI.Docking;

namespace CommandsEditor.ConfigEditors
{
    public partial class ViewconeEditor : BaseWindow
    {
        BML _activeSet = null;

        //Note to self, I think the engine supports adding in new sets. If I add one it needs adding to the difficulty settings 'ViewconeSets' too.

        public ViewconeEditor() : base()
        {
            InitializeComponent();

            BML viewconeTypes = new BML(Singleton.PathToAI + "\\DATA\\VIEW_CONE_SETS\\VIEWCONESETS.BML");
            var viewcones = viewconeTypes.Content["ViewconeSets"];
            viewconeSets.BeginUpdate();
            foreach (XmlElement viewcone in viewcones)
            {
                string name = viewcone["Name"].InnerText;
                switch (name)
                {
                    case "VIEWCONESET_STANDARD":
                    case "VIEWCONESET_HUMAN":
                    case "VIEWCONESET_SLEEPING": //unused? it doesnt have all sets.
                    case "VIEWCONESET_ANDROID":
                        break;
                    default:
                        // It appears the game skips any other than the ones above, so ignore them.
                        continue;
                }
                viewconeSets.Items.Add(name);
            }
            viewconeSets.EndUpdate();

            this.FormClosing += ViewconeEditor_FormClosing;
            Singleton.OnResetConfigs += () => { this.Close(); };
        }

        private void ViewconeEditor_Load(object sender, EventArgs e)
        {
            viewconeSets.SelectedIndex = 0;
        }

        private void ViewconeEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(this.Controls, Save);
            this.FormClosing -= ViewconeEditor_FormClosing;
        }

        private void viewconeSets_SelectedIndexChanged(object sender, EventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(this.Controls, Save);
            tabControl1.SuspendLayout();

            _activeSet = new BML(Singleton.PathToAI + "\\DATA\\VIEW_CONE_SETS\\" + viewconeSets.Text + ".BML");
            var viewcones = _activeSet.Content["ViewconeSet"]["ViewconeSettings"];
            tabControl1.TabPages.Clear();
            foreach (XmlElement viewcone in viewcones)
            {
                TabPage page = new TabPage();
                ViewCone editor = new ViewCone();
                page.Controls.Add(editor);
                tabControl1.TabPages.Add(page);

                page.Text = viewcone["ViewconeSettings_type"].InnerText;
                editor.Populate(viewcone);
            }

            tabControl1.ResumeLayout();
            ConfigEditorUtils.Subscribe(this.Controls, Save);
        }
        
        private void Save(object sender, EventArgs e)
        {
            var doc = _activeSet.Content;

            doc["ViewconeSet"]["ViewconeSettings"].RemoveAll();
            foreach (TabPage page in tabControl1.TabPages)
            {
                XmlElement set = doc.CreateElement("steeringBoundary");
                page.Controls.OfType<ViewCone>().FirstOrDefault().Save(set);
                doc["ViewconeSet"]["ViewconeSettings"].AppendChild(set);
            }

            _activeSet.Content = doc;
            _activeSet.Save();

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void helpBtn_Click(object sender, EventArgs e)
        {
            Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
            Process.Start("https://opencage.co.uk/docs/configs/viewcones");
        }
    }
}

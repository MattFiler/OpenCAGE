using CATHODE;
using CommandsEditor.Popups.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using WeifenLuo.WinFormsUI.Docking;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace CommandsEditor.ConfigEditors
{
    public partial class BlueprintEditor : BaseWindow
    {
        private readonly BML _gblItem;

        public BlueprintEditor() : base()
        {
            InitializeComponent();

            _gblItem = new BML(Singleton.PathToAI + @"\DATA\GBL_ITEM.BML");

            var recipes = _gblItem.Content["item_database"]["recipes"];
            foreach (XmlElement recipe in recipes)
            {
                blueprints.Items.Add(recipe.GetAttribute("name"));
            }
            blueprints.SelectedIndex = 0;

            Singleton.OnResetConfigs += () => { this.Close(); };
        }

        private void blueprints_SelectedIndexChanged(object sender, EventArgs e)
        {
            var recipes = _gblItem.Content["item_database"]["recipes"];
            blueprintInput.BeginUpdate();
            blueprintOutput.BeginUpdate();
            foreach (XmlElement recipe in recipes)
            {
                if (recipe.GetAttribute("name") != blueprints.Text)
                    continue;

                blueprintInput.Items.Clear();
                blueprintOutput.Items.Clear();

                var inputs = recipe["input"];
                foreach (XmlElement input in inputs)
                {
                    ListViewItem item = new ListViewItem(input.GetAttribute("name"));
                    item.SubItems.Add(input.GetAttribute("quantity"));
                    blueprintInput.Items.Add(item);
                }

                var outputs = recipe["output"];
                foreach (XmlElement output in outputs)
                {
                    ListViewItem item = new ListViewItem(output.GetAttribute("name"));
                    item.SubItems.Add(output.GetAttribute("quantity"));
                    blueprintOutput.Items.Add(item);
                }
            }
            blueprintInput.EndUpdate();
            blueprintOutput.EndUpdate();
        }

        private void addNewItemRequired_Click(object sender, EventArgs e)
        {
            BlueprintEditorPopup editorPopup = new BlueprintEditorPopup(1);
            editorPopup.Show();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            BlueprintEditorPopup editorPopup = new BlueprintEditorPopup(2);
            editorPopup.Show();
        }

        public void getDataFromPopup(string NEW_QUANTITY, string NEW_ITEM, int DATA_TYPE)
        {
            ListViewItem item = new ListViewItem(NEW_ITEM);
            item.SubItems.Add(NEW_QUANTITY);

            if (DATA_TYPE == 1)
                blueprintInput.Items.Add(item);
            else
                blueprintOutput.Items.Add(item);
        }

        private void removeInputItem_Click(object sender, EventArgs e)
        {
            if (blueprintInput.SelectedItems.Count == 0)
                return;

            blueprintInput.Items.Remove(blueprintInput.SelectedItems[0]);
        }

        private void removeOutputItem_Click(object sender, EventArgs e)
        {
            if (blueprintOutput.SelectedItems.Count == 0)
                return;

            blueprintOutput.Items.Remove(blueprintOutput.SelectedItems[0]);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            var doc = _gblItem.Content;

            var recipes = doc["item_database"]["recipes"];
            foreach (XmlElement recipe in recipes)
            {
                if (recipe.GetAttribute("name") != blueprints.Text)
                    continue;

                recipe.RemoveAll();
                recipe.SetAttribute("name", blueprints.Text);

                List<Tuple<string, string>> inputItems = new List<Tuple<string, string>>();
                for (int i = 0; i < blueprintInput.Items.Count; i++)
                    inputItems.Add(new Tuple<string, string>(blueprintInput.Items[i].Text, blueprintInput.Items[i].SubItems[1].Text));
                AddRecipeParts(doc, "input", inputItems, recipe);

                List<Tuple<string, string>> outputItems = new List<Tuple<string, string>>();
                for (int i = 0; i < blueprintOutput.Items.Count; i++)
                    outputItems.Add(new Tuple<string, string>(blueprintOutput.Items[i].Text, blueprintOutput.Items[i].SubItems[1].Text));
                AddRecipeParts(doc, "output", outputItems, recipe);
            }

            _gblItem.Content = doc;
            _gblItem.Save();

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void AddRecipeParts(XmlDocument doc, string itemType, List<Tuple<string, string>> items, XmlElement parent)
        {
            XmlElement type = doc.CreateElement(itemType);
            foreach (Tuple<string, string> item in items)
            {
                XmlElement itemElement = doc.CreateElement("item");
                itemElement.SetAttribute("name", item.Item1);
                itemElement.SetAttribute("quantity", item.Item2);
                type.AppendChild(itemElement);
            }
            parent.AppendChild(type);
        }

        private void helpBtn_Click(object sender, EventArgs e)
        {
            Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
            Process.Start("https://opencage.co.uk/docs/configs/blueprint-recipes");
        }
    }
}

using CATHODE;
using CommandsEditor.Popups.Base;
using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Xml;

namespace CommandsEditor.ConfigEditors
{
    public partial class PhysicalMaterialEditor : BaseWindow
    {
        BML _materialTypes;

        public PhysicalMaterialEditor() : base()
        {
            InitializeComponent();

            _materialTypes = new BML(Singleton.PathToAI + "\\DATA\\MATERIAL_DATA\\MATERIALS.BML");
            var materials = _materialTypes.Content["Materials"];
            materialList.BeginUpdate();
            foreach (XmlElement material in materials)
            {
                materialList.Items.Add(material.GetAttribute("name"));
            }
            materialList.EndUpdate();

            Singleton.OnResetConfigs += () => { this.Close(); };
        }

        AddNewPhysMaterial _addMat = null;
        private void addNew_Click(object sender, EventArgs e)
        {
            if (_addMat != null)
            {
                _addMat.OnMaterialAdded -= OnMaterialAdded;
                _addMat.FormClosed -= _addMat_FormClosed;
                _addMat.Close();
            }

            _addMat = new AddNewPhysMaterial();
            _addMat.Show();
            _addMat.OnMaterialAdded += OnMaterialAdded;
            _addMat.FormClosed += _addMat_FormClosed;
        }
        private void OnMaterialAdded(string mat)
        {
            for (int i =0; i < materialList.Items.Count; i++) 
                if (materialList.Items[i].ToString() == mat)
                    return;

            materialList.Items.Add(mat);
            Save();
        }
        private void _addMat_FormClosed(object sender, FormClosedEventArgs e)
        {
            _addMat.OnMaterialAdded -= OnMaterialAdded;
            _addMat.FormClosed -= _addMat_FormClosed;
            _addMat = null;
        }

        private void removeSelected_Click(object sender, EventArgs e)
        {
            if (materialList.SelectedIndex == -1)
                return;

            //note - the physical material is looked up by index, so removing any here will be problematic. should support that better.

            materialList.Items.RemoveAt(materialList.SelectedIndex);
            Save();
        }

        private void Save()
        {
            var doc = _materialTypes.Content;

            doc["Materials"].RemoveAll();
            for (int i = 0; i < materialList.Items.Count; i++)
            {
                XmlElement mat = doc.CreateElement("Material");
                mat.SetAttribute("name", materialList.Items[i].ToString());
                doc["Materials"].AppendChild(mat);
            }

            _materialTypes.Content = doc;
            _materialTypes.Save();

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void helpBtn_Click(object sender, EventArgs e)
        {
            Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
            Process.Start("https://opencage.co.uk/docs/configs/physical-materials");
        }
    }
}

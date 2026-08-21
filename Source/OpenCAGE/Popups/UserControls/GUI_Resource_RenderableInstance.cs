using AlienPAK;
using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Popups.UserControls
{
    public partial class GUI_Resource_RenderableInstance : ResourceUserControl
    {
        private ResourceReference _resourceRef;
        private Models.CS2.Component.LOD.Submesh _selectedModelParent = null;
        private List<Materials.Material> _selectedMaterials = new List<Materials.Material>();


        public GUI_Resource_RenderableInstance() : base()
        {
            InitializeComponent();

            this.Disposed += GUI_Resource_RenderableInstance_Disposed;
        }

        private void GUI_Resource_RenderableInstance_Disposed(object sender, EventArgs e)
        {
            _matEditor?.Close();
        }

        public override void PopulateUI(ResourceReference resource)
        {
            _resourceRef = resource;

            //Position/rotation aren't shown: they're derived from the owning entity's transform on save
            PopulateUI(resource.RenderableInstance);
        }
        public void PopulateUI(List<RenderableElements.Element> renderables)
        {
            _selectedModelParent = renderables.Count == 0 ? null : renderables[0].Model;
            _selectedMaterials.Clear();
            for (int i = 0; i < renderables.Count; i++)
                _selectedMaterials.Add(renderables[i].Material == null ? Singleton.FallbackMaterial : renderables[i].Material);

            if (_selectedModelParent == null)
                return;

            Models.CS2.Component.LOD lod = Content.Level.Models.FindModelLOD(_selectedModelParent); 
            //Models.CS2.Component component = Editor.resource.models.FindModelComponentForSubmesh(submesh);
            Models.CS2 mesh = Content.Level.Models.FindModel(_selectedModelParent);

            if (mesh == null)
                return;

            modelInfoTextbox.Text = mesh.Name;
            if (lod.Name != "")
                modelInfoTextbox.Text += " -> [" + lod.Name + "]"; 

            materials.Items.Clear();
            for (int i = 0; i < _selectedMaterials.Count; i++)
                materials.Items.Add(/*"[" + mesh.Submeshes[i].Name + "] " + */_selectedMaterials[i].Name);

        }
        private void editModel_Click(object sender, EventArgs e)
        {
            EditModel selectModel = new EditModel(_selectedModelParent);
            selectModel.Show();
            selectModel.OnModelSelected += ModelSelected;
        }
        private void ModelSelected(Models.CS2.Component model)
        {
            this.BringToFront();
            this.Focus();

            PopulateUI(model.ToRenderableElements());

            UpdateResource();
        }

        private int _selectedIndex = -1;
        private EditMaterial _matEditor = null;

        private void editMaterial_Click(object sender, EventArgs e)
        {
            if (materials.SelectedIndex == -1) return;

            _selectedIndex = materials.SelectedIndex;

            if (_matEditor != null)
                _matEditor.Close();

            _matEditor = new EditMaterial(_selectedMaterials[materials.SelectedIndex]);
            _matEditor.FormClosed += Editor_FormClosed;
            _matEditor.OnMaterialSelected += MaterialSelected;
            _matEditor.Show();
        }

        private void Editor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _matEditor = null;
        }

        private void MaterialSelected(Materials.Material material)
        {
            this.BringToFront();
            this.Focus();

            _selectedMaterials[_selectedIndex] = material;
            materials.Items[_selectedIndex] = material.Name;
            materials.SelectedIndex = _selectedIndex;

            UpdateResource();
        }

        private void UpdateResource()
        {
            if (_resourceRef == null) return;
            _resourceRef.RenderableInstance = GetAsRenderableElements();
            Singleton.OnResourceModified?.Invoke();
        }

        public List<RenderableElements.Element> GetAsRenderableElements()
        {
            Models.CS2.Component component = Content.Level.Models.FindModelComponent(_selectedModelParent);
            List<RenderableElements.Element> reds = component.ToRenderableElements();
            for (int i = 0; i < reds.Count; i++)
            {
                //NOTE: Currently only remapping TOP LOD materials. Wrong wrong wrong!!
                reds[i].Material = _selectedMaterials[i];
            }
            Content.Level.RenderableElements.Entries.AddRange(reds);
            return reds;
        }

    }
}



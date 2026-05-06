using CATHODE.Scripting.Internal;
using CATHODE.Scripting;
using CommandsEditor.Popups.UserControls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Media3D;
using CathodeLib;
using System.Numerics;
using CommandsEditor.Popups.Base;
using CommandsEditor.DockPanels;
using WeifenLuo.WinFormsUI.Docking;

namespace CommandsEditor
{
    public partial class Composite3D : DockContent
    {
        GUI_ModelViewer modelViewer;
        CompositeDisplay _compositeDisplay;

        public Composite3D(CompositeDisplay compositeDisplay)
        {
            _compositeDisplay = compositeDisplay;

            InitializeComponent();
            this.Text += ": " + _compositeDisplay.Composite.name;

            List<GUI_ModelViewer.Model> models = new List<GUI_ModelViewer.Model>();
            models.AddRange(LoadComposite(_compositeDisplay.Composite));

            modelViewer = new GUI_ModelViewer();
            modelRendererHost.Child = modelViewer;
            modelViewer.ShowModel(models);
        }

        private List<GUI_ModelViewer.Model> LoadComposite(Composite comp, cTransform offset = null)
        {
            List<GUI_ModelViewer.Model> models = new List<GUI_ModelViewer.Model>();
            if (comp == null) return models;

            ReadOnlyEntityCollection<FunctionEntity> entities = comp.functions;
            foreach (FunctionEntity entity in entities)
            {
                Parameter position = entity.GetParameter("position");
                cTransform globalPosition = ((cTransform)position?.content) + offset;

                if (!entity.function.IsFunctionType)
                    models.AddRange(LoadComposite(_compositeDisplay.Content.Level.Commands.GetComposite(entity.function), globalPosition));

                Parameter resource = entity.GetParameter("resource");
                if (resource == null) continue;
                List<ResourceReference> resourceRefs = ((cResource)(resource.content)).value;
                foreach (ResourceReference resourceRef in resourceRefs.Where(o => o.resource_type == ResourceType.RENDERABLE_INSTANCE))
                    for (int i = 0; i < resourceRef.RenderableInstance.Count; i++)
                        models.Add(new GUI_ModelViewer.Model(resourceRef.RenderableInstance[i].Model, globalPosition));
            }
            return models;
        }
    }
}

using CATHODE.Scripting;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class InstanceSelection : BaseWindow
    {
        public Action<ShortGuid> OnInstanceSelected;

        private List<EntityPath> _hierarchies = new List<EntityPath>();

        public InstanceSelection(EntityInspector editor, List<ShortGuid> existing) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent(); 
            
            List<EntityPath> hierarchies = editor.Content.EditorUtils.GetHierarchiesForEntity(editor.Composite, editor.Entity);
            for (int i = 0; i < hierarchies.Count; i++)
            {
                if (existing.Contains(hierarchies[i].GenerateCompositeInstanceID())) continue;
                instances.Items.Add(Content.Level.Commands.Utils.GetResolvedAsString(Content.Level.Commands.Utils.ResolveHierarchy(hierarchies[i]), SettingsManager.GetBool(Settings.ShowShortGuids)));
                _hierarchies.Add(hierarchies[i]);
            }

            if (instances.Items.Count == 0)
            {
                MessageBox.Show("There are no instances to be populated!", "Instances populated.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            else instances.SelectedIndex = 0;
        }

        private void addCharacter_Click(object sender, EventArgs e)
        {
            if (instances.SelectedIndex == -1) return;
            OnInstanceSelected?.Invoke(_hierarchies[instances.SelectedIndex].GenerateCompositeInstanceID());
            this.Close();
        }
    }
}

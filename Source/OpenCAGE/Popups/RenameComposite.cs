using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;
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
using System.Windows.Input;

namespace OpenCAGE
{
    public partial class RenameComposite : BaseWindow
    {
        private Composite _composite;

        public RenameComposite(Composite composite) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();
            
            _composite = composite;
            entity_name.Text = EditorUtils.GetCompositeName(_composite);
        }

        private void save_entity_name_Click(object sender, EventArgs e)
        {
            if (entity_name.Text == "") return;

            int nameLength = EditorUtils.GetCompositeName(_composite).Length;
            string path = (_composite.name.Length != nameLength ? _composite.name.Substring(0, _composite.name.Length - nameLength - 1) + "/" : "") + entity_name.Text.Replace("\\", "/"); 

            string[] pathParts = path.Split('/');
            for (int i = 0; i < pathParts.Length; i++)
            {
                if (pathParts[i] == "")
                {
                    MessageBox.Show("Failed to create composite: a part of the path is blank.\nRemove trailing slashes and use complete folder names, e.g.:\nSOME/FILE/PATH/TO/COMPOSITE", "Composite path/name invalid", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            for (int i = 0; i < Content.Level.Commands.Entries.Count; i++)
            {
                if (Content.Level.Commands.Entries[i].name.Replace("\\", "/") == path)
                {
                    MessageBox.Show("Failed to create composite.\nA composite with this name already exists.", "Composite already exists", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            //--

            //Update composite name
            _composite.name = path.Replace("/", "\\");

            //Update cached ListViewItems that instance this composite
            Content.Level.Commands.Entries.ForEach(composite =>
            {
                composite.functions_dictionary.Values.Where(o => o.function == _composite.shortGUID).ToList().ForEach(function => 
                {
                    Content.GenerateListViewItem(function, composite, LevelContent.CacheMethod.IGNORE_AND_OVERWRITE_CACHE);
                });
            });

            //Fire off an event so any UI that references the name can update
            Singleton.OnCompositeRenamed?.Invoke(_composite, path);

            this.Close();
        }
    }
}

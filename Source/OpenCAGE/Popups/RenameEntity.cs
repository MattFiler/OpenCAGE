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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace OpenCAGE
{
    public partial class RenameEntity : BaseWindow
    {
        private Entity _entity;
        private Composite _composite;

        public RenameEntity(Entity entity, Composite composite) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();

            _entity = entity;
            _composite = composite;

            switch (_entity.variant)
            {
                case EntityVariant.VARIABLE:
                    entity_name.Text = ShortGuidUtils.FindString(((VariableEntity)_entity).name);
                    break;
                default:
                    entity_name.Text = Content.Level.Commands.Utils.GetEntityName(_composite, _entity);
                    break;
            }
        }

        private void save_entity_name_Click(object sender, EventArgs e)
        {
            if (entity_name.Text == "") return;

            //Update entity name
            switch (_entity.variant)
            {
                case EntityVariant.VARIABLE:
                    ((VariableEntity)_entity).name = ShortGuidUtils.Generate(entity_name.Text);
                    break;
                default:
                    Content.Level.Commands.Utils.SetEntityName(_composite, _entity, entity_name.Text);
                    break;
            }

            //Update cached entity ListViewItem
            Content.GenerateListViewItem(_entity, _composite, LevelContent.CacheMethod.IGNORE_AND_OVERWRITE_CACHE);

            //Update cached proxy/alias ListViewItems that contain this entity
            Content.Level.Commands.Entries.ForEach(composite =>
            {
                composite.proxies_dictionary.Values.Where(o => o.proxy.path.Contains(_entity.shortGUID)).ToList().ForEach(proxy => 
                {
                    Content.GenerateListViewItem(proxy, composite, LevelContent.CacheMethod.IGNORE_AND_OVERWRITE_CACHE);
                });
                composite.aliases_dictionary.Values.Where(o => o.alias.path.Contains(_entity.shortGUID)).ToList().ForEach(alias =>
                {
                    Content.GenerateListViewItem(alias, composite, LevelContent.CacheMethod.IGNORE_AND_OVERWRITE_CACHE);
                });
            });

            //Fire off an event so any UI that references the name can update
            Singleton.OnEntityRenamed?.Invoke(_entity, entity_name.Text);

            this.Close();
        }
    }
}

using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CommandsEditor.DockPanels;
using CommandsEditor.Popups.Base;
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

namespace CommandsEditor
{
    public partial class RenameGeneric : BaseWindow
    {
        public Action<string> OnRenamed;

        public RenameGeneric(string initialText, RenameGenericContent content) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();
            
            entity_name.Text = initialText;

            this.Text = content.Title;
            label1.Text = content.Description;
            save_entity_name.Text = content.ButtonText;
        }

        private void save_entity_name_Click(object sender, EventArgs e)
        {
            if (entity_name.Text == "") return;

            OnRenamed?.Invoke(entity_name.Text);
            this.Close();
        }

        public class RenameGenericContent
        {
            public string Title;
            public string Description;
            public string ButtonText;
        }
    }
}

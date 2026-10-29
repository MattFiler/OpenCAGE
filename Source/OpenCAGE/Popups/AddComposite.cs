using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;

namespace OpenCAGE
{
    public partial class AddComposite : BaseWindow
    {
        public Action<Composite> OnCompositeAdded;

        CompositeBrowser _commands;
        string _folder;

        public AddComposite(CompositeBrowser editor, string folderPath) : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            _commands = editor;
            InitializeComponent();
            OpenCAGE.Popups.Base.BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/scripting-intro/");

            _folder = folderPath;
            this.Text = _folder == "" ? "Create New Composite" : "Create New Composite In Folder '" + _folder + "'";
            textInput.Select();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (textInput.Text == "") return;

            string path = (_folder == "" ? _folder : _folder + "/") + textInput.Text.Replace("\\", "/");

            string[] pathParts = path.Split('/');
            for (int i = 0; i < pathParts.Length; i++)
            {
                if (pathParts[i] == "")
                {
                    MessageBox.Show("Failed to create composite: a part of the path is blank.\nRemove trailing slashes and use complete folder names, e.g.:\nSOME/FILE/PATH/TO/COMPOSITE", "Composite path/name invalid", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            //A composite's name is used as a path: a character a path can't hold breaks the editor's handling of it
            char bad = path.FirstOrDefault(c => System.IO.Path.GetInvalidPathChars().Contains(c));
            if (bad != default(char))
            {
                MessageBox.Show("Failed to create composite: its name can't contain " + (char.IsControl(bad) ? "control characters" : "'" + bad + "'") + ".", "Composite path/name invalid", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string nameProblem = EditorUtils.UnstorableTextProblem(path, "This composite name");
            if (nameProblem != null)
            {
                MessageBox.Show(nameProblem, "Composite path/name invalid", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            for (int i = 0; i < _commands.Content.Level.Commands.Entries.Count; i++)
            {
                if (_commands.Content.Level.Commands.Entries[i].name.Replace("\\", "/") == path)
                {
                    MessageBox.Show("Failed to create composite.\nA composite with this name already exists.", "Composite already exists", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            Singleton.OnCompositeAddPending?.Invoke();

            Composite comp = _commands.Content.Level.Commands.AddComposite(path.Replace("/", "\\"));

            Singleton.OnCompositeAdded?.Invoke(comp);
            OpenCAGE.Undo.UndoStack.Current.Record(new OpenCAGE.Undo.CompositeAddEdit(comp, false, "Add composite " + EditorUtils.GetCompositeName(comp)));
            Steam.CountTowards(Steam.Achievements.ONE_HUNDRED_COMPOSITES);
            OnCompositeAdded?.Invoke(comp);
            this.Close();
        }

        private void OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                submitBtn.PerformClick();
        }
    }
}

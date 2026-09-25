using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.Scripting.Refactor;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// What a De-instance or Create Composite will do before it is done: anything that stops it, anything it
    /// cannot carry over exactly, and - for a new composite - its name.
    /// </summary>
    public partial class RefactorDialog : BaseWindow
    {
        /// <summary>The Create Composite plan for the name that was accepted, once OK is pressed.</summary>
        public CreateCompositePlan CreatePlan { get; private set; }

        private Commands _commands;
        private Composite _parent;
        private List<Entity> _selection;
        private CreateCompositePlan _selectionPlan;

        private RefactorDialog() : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            InitializeComponent();
            StayAboveEditor = true;
        }

        public static RefactorDialog ForDeinstance(DeinstancePlan plan, string instanceName)
        {
            RefactorDialog dialog = new RefactorDialog();
            dialog.Text = "De-instance '" + instanceName + "'";
            int count = plan.Content == null ? 0 : plan.Content.functions_dictionary.Count + plan.Content.aliases_dictionary.Count + plan.Content.proxies_dictionary.Count;
            dialog.headerLabel.Text = plan.Content == null
                ? "'" + instanceName + "' is not an instance of a composite in this level."
                : "Pull the " + count + " entities of '" + instanceName + "' (" + EditorUtils.GetCompositeName(plan.Content) + ") out into " + EditorUtils.GetCompositeName(plan.Parent) + ", placed where the instance put them, and remove the instance. " + EditorUtils.GetCompositeName(plan.Content) + " itself is left as it is.";
            dialog.HideName();
            dialog.ShowIssues(plan.Issues, null);
            dialog.okButton.Text = "De-instance";
            dialog.okButton.Enabled = plan.CanApply;
            return dialog;
        }

        public static RefactorDialog ForCreateComposite(Commands commands, Composite parent, List<Entity> selection, string defaultName)
        {
            RefactorDialog dialog = new RefactorDialog();
            dialog._commands = commands;
            dialog._parent = parent;
            dialog._selection = selection;
            dialog.Text = "Create Composite";
            dialog.headerLabel.Text = "Move the " + selection.Count + " selected " + (selection.Count == 1 ? "entity" : "entities") + " out of " + EditorUtils.GetCompositeName(parent) + " into a new composite, and place an instance of it where they were. Links to what stays behind go through the new composite's parameters.";
            dialog.okButton.Text = "Create";

            //Worked out once for the selection; the name is checked as it is typed, and the final plan made on OK
            Cursor previous = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                dialog._selectionPlan = CreateCompositePlan.Plan(commands, parent, selection, defaultName);
            }
            finally
            {
                Cursor.Current = previous;
            }
            dialog.nameInput.Text = defaultName;
            dialog.nameInput.TextChanged += (sender, e) => dialog.RefreshCreate();
            dialog.RefreshCreate();
            dialog.nameInput.Select();
            dialog.nameInput.SelectAll();
            return dialog;
        }

        private void RefreshCreate()
        {
            string nameProblem = CreateCompositePlan.CheckName(_commands, nameInput.Text);
            ShowIssues(_selectionPlan.Issues, nameProblem);
            okButton.Enabled = nameProblem == null && _selectionPlan.CanApply;
        }

        private void HideName()
        {
            int shift = issuesLabel.Top - nameLabel.Top;
            nameLabel.Visible = false;
            nameInput.Visible = false;
            issuesLabel.Top -= shift;
            issuesText.Top -= shift;
            issuesText.Height += shift;
        }

        private void ShowIssues(List<RefactorIssue> issues, string extraProblem)
        {
            List<string> blocking = issues.Where(o => o.Blocking).Select(o => o.Message).ToList();
            if (extraProblem != null)
                blocking.Insert(0, extraProblem);
            List<string> notes = issues.Where(o => !o.Blocking).Select(o => o.Message).ToList();

            StringBuilder text = new StringBuilder();
            if (blocking.Count != 0)
            {
                text.Append("This can't be done:").Append(Environment.NewLine);
                foreach (string message in blocking)
                    text.Append("  • ").Append(message).Append(Environment.NewLine);
                if (notes.Count != 0)
                    text.Append(Environment.NewLine);
            }
            if (notes.Count != 0)
            {
                text.Append(blocking.Count == 0 ? "It can be done, but not everything carries over exactly:" : "Also:").Append(Environment.NewLine);
                foreach (string message in notes)
                    text.Append("  • ").Append(message).Append(Environment.NewLine);
            }
            if (blocking.Count == 0 && notes.Count == 0)
                text.Append("Everything carries over exactly.");
            issuesLabel.Text = blocking.Count != 0 ? "Why not" : "Before you go ahead";
            issuesText.Text = text.ToString().TrimEnd();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            if (_selectionPlan != null)
            {
                Cursor previous = Cursor.Current;
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    CreatePlan = CreateCompositePlan.Plan(_commands, _parent, _selection, nameInput.Text);
                }
                finally
                {
                    Cursor.Current = previous;
                }
                if (!CreatePlan.CanApply)
                {
                    ShowIssues(CreatePlan.Issues, null);
                    okButton.Enabled = false;
                    return;
                }
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
